using Microsoft.Win32;
using System.IO;
using MicrosoftPinyinCleaner.Models;

namespace MicrosoftPinyinCleaner.Services;

public sealed class StartupService
{
    public const string StartupValueName = "MicrosoftPinyinCleaner";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public StartupStatus GetStatus()
    {
        try
        {
            var expectedCommand = GetCurrentCommand();
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var currentCommand = key?.GetValue(StartupValueName) as string;

            if (string.IsNullOrWhiteSpace(currentCommand))
            {
                return new StartupStatus(false, false, "登录自动清理未开启。");
            }

            if (string.Equals(currentCommand, expectedCommand, StringComparison.Ordinal))
            {
                return new StartupStatus(true, false, "登录自动清理已开启。");
            }

            return new StartupStatus(false, true, "启动项中的程序路径已变化，请重新开启自动清理。");
        }
        catch (Exception ex)
        {
            return new StartupStatus(false, false, $"读取启动项失败：{GetSafeMessage(ex)}");
        }
    }

    public OperationResult Enable()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                return OperationResult.Fail("无法打开当前用户启动项注册表。");
            }

            key.SetValue(StartupValueName, GetCurrentCommand(), RegistryValueKind.String);
            return OperationResult.Ok("已开启登录自动清理，下次登录时生效。");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"开启登录自动清理失败：{GetSafeMessage(ex)}");
        }
    }

    public OperationResult Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(StartupValueName, throwOnMissingValue: false);
            return OperationResult.Ok("已关闭登录自动清理。程序和其他设置未被删除。");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"关闭登录自动清理失败：{GetSafeMessage(ex)}");
        }
    }

    private static string GetCurrentCommand()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("无法确定当前程序路径。");
        }

        return StartupCommandBuilder.Build(Path.GetFullPath(executablePath));
    }

    private static string GetSafeMessage(Exception exception) =>
        string.IsNullOrWhiteSpace(exception.Message) ? "未知错误。" : exception.Message;
}
