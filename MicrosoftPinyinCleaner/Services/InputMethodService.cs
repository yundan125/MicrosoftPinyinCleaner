using System.Text.Json;
using MicrosoftPinyinCleaner.Models;

namespace MicrosoftPinyinCleaner.Services;

public sealed class InputMethodService
{
    private const string DetectScript = """
        $ErrorActionPreference = 'Stop'
        try {
            $languages = @()
            foreach ($language in @(Get-WinUserLanguageList)) {
                $tips = @()
                foreach ($tip in @($language.InputMethodTips)) {
                    $tips += [string]$tip
                }
                $languages += [pscustomobject]@{
                    LanguageTag = [string]$language.LanguageTag
                    InputMethodTips = $tips
                }
            }
            [pscustomobject]@{ Languages = $languages } | ConvertTo-Json -Compress -Depth 4
        }
        catch {
            [Console]::Error.WriteLine($_.Exception.Message)
            exit 1
        }
        """;

    private static readonly string RemoveScript = $$"""
        $ErrorActionPreference = 'Stop'
        $targetTip = '{{InputMethodPolicy.MicrosoftPinyinTip}}'
        try {
            $list = Get-WinUserLanguageList
            $found = 0
            $otherChinese = 0
            $remainingTotal = 0

            foreach ($language in $list) {
                $isChinese = ([string]$language.LanguageTag) -match '^zh($|-)'
                foreach ($tip in @($language.InputMethodTips)) {
                    if ([string]::Equals(([string]$tip).Trim(), $targetTip, [System.StringComparison]::OrdinalIgnoreCase)) {
                        $found++
                    }
                    else {
                        $remainingTotal++
                        if ($isChinese) { $otherChinese++ }
                    }
                }
            }

            if ($found -eq 0) {
                [pscustomobject]@{ Outcome = 'NotInstalled' } | ConvertTo-Json -Compress
                exit 0
            }

            if ($otherChinese -lt 1 -or $remainingTotal -lt 1) {
                [pscustomobject]@{ Outcome = 'BlockedNoAlternative' } | ConvertTo-Json -Compress
                exit 0
            }

            foreach ($language in $list) {
                $remainingTips = @($language.InputMethodTips | Where-Object {
                    -not [string]::Equals(([string]$_).Trim(), $targetTip, [System.StringComparison]::OrdinalIgnoreCase)
                })
                $language.InputMethodTips.Clear()
                foreach ($tip in $remainingTips) {
                    [void]$language.InputMethodTips.Add([string]$tip)
                }
            }

            $verifiedRemaining = 0
            foreach ($language in $list) { $verifiedRemaining += @($language.InputMethodTips).Count }
            if ($verifiedRemaining -lt 1) { throw '安全检查失败：操作会清空输入法列表。' }

            Set-WinUserLanguageList -LanguageList $list -Force
            [pscustomobject]@{ Outcome = 'Removed' } | ConvertTo-Json -Compress
        }
        catch {
            [Console]::Error.WriteLine($_.Exception.Message)
            exit 1
        }
        """;

    private readonly PowerShellRunner _runner = new();

    public async Task<DetectionResult> DetectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _runner.RunEncodedScriptAsync(DetectScript, cancellationToken);
            if (result.TimedOut)
            {
                return DetectionFailure("检测超时，请稍后重试。");
            }

            if (result.ExitCode != 0)
            {
                return DetectionFailure(ToFriendlyPowerShellError(result.StandardError, "读取输入法列表失败。"));
            }

            var payload = JsonSerializer.Deserialize<LanguageListPayload>(
                result.StandardOutput,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (payload?.Languages is null)
            {
                return DetectionFailure("未能识别 PowerShell 返回的输入法列表。");
            }

            var languages = payload.Languages.Select(language => new InputMethodLanguage(
                language.LanguageTag ?? string.Empty,
                language.InputMethodTips ?? Array.Empty<string>())).ToArray();
            var analysis = InputMethodPolicy.Analyze(languages);

            return analysis.HasMicrosoftPinyin
                ? new DetectionResult(MicrosoftPinyinState.Installed, analysis.CanSafelyRemove, "检测到微软拼音。")
                : new DetectionResult(MicrosoftPinyinState.NotInstalled, false, "当前用户输入法列表中没有微软拼音。");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return DetectionFailure(ToFriendlyException(ex, "检测输入法时发生错误。"));
        }
    }

    public async Task<RemovalResult> RemoveMicrosoftPinyinAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _runner.RunEncodedScriptAsync(RemoveScript, cancellationToken);
            if (result.TimedOut)
            {
                return new RemovalResult(RemovalOutcome.Failed, "处理超时，未确认输入法列表已更改。请重新检测。");
            }

            if (result.ExitCode != 0)
            {
                return new RemovalResult(
                    RemovalOutcome.Failed,
                    ToFriendlyPowerShellError(result.StandardError, "删除微软拼音失败，输入法列表未被清空。"));
            }

            var payload = JsonSerializer.Deserialize<RemovalPayload>(
                result.StandardOutput,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return payload?.Outcome switch
            {
                "Removed" => new RemovalResult(RemovalOutcome.Removed, "已从当前用户输入法列表中移除微软拼音。"),
                "NotInstalled" => new RemovalResult(RemovalOutcome.NotInstalled, "微软拼音未安装，无需删除。"),
                "BlockedNoAlternative" => new RemovalResult(
                    RemovalOutcome.BlockedNoAlternative,
                    "为避免失去中文输入能力，已阻止删除。请先安装并启用其他中文输入法。"),
                _ => new RemovalResult(RemovalOutcome.Failed, "未能确认删除结果，输入法列表未被清空。")
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new RemovalResult(RemovalOutcome.Failed, ToFriendlyException(ex, "删除微软拼音时发生错误。"));
        }
    }

    private static DetectionResult DetectionFailure(string message) =>
        new(MicrosoftPinyinState.DetectionFailed, false, message);

    private static string ToFriendlyPowerShellError(string error, string fallback)
    {
        var firstLine = error.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.Trim();
        return string.IsNullOrWhiteSpace(firstLine) ? fallback : $"{fallback} {Limit(firstLine, 180)}";
    }

    private static string ToFriendlyException(Exception exception, string fallback) =>
        exception is InvalidOperationException && !string.IsNullOrWhiteSpace(exception.Message)
            ? exception.Message
            : fallback;

    private static string Limit(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength] + "…";

    private sealed class LanguageListPayload
    {
        public LanguagePayload[]? Languages { get; init; }
    }

    private sealed class LanguagePayload
    {
        public string? LanguageTag { get; init; }
        public string[]? InputMethodTips { get; init; }
    }

    private sealed class RemovalPayload
    {
        public string? Outcome { get; init; }
    }
}
