using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace MicrosoftPinyinCleaner.Services;

internal sealed record PowerShellResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut);

internal sealed class PowerShellRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public async Task<PowerShellResult> RunEncodedScriptAsync(
        string script,
        CancellationToken cancellationToken = default)
    {
        var executable = ResolvePowerShellExecutable();
        var encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(encodedScript);

        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("无法启动 PowerShell。请确认系统组件可用。");
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            throw new InvalidOperationException("无法启动 PowerShell。请确认 Windows PowerShell 可用。", ex);
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeoutSource = new CancellationTokenSource(DefaultTimeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        try
        {
            await process.WaitForExitAsync(linkedSource.Token);
            var output = await outputTask;
            var error = await errorTask;
            return new PowerShellResult(process.ExitCode, output, error, false);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            await DrainAsync(outputTask, errorTask);
            return new PowerShellResult(-1, string.Empty, "PowerShell 操作超时。", true);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            await DrainAsync(outputTask, errorTask);
            throw;
        }
    }

    private static string ResolvePowerShellExecutable()
    {
        var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var systemPowerShell = Path.Combine(systemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        return File.Exists(systemPowerShell) ? systemPowerShell : "powershell.exe";
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // The process may have exited between the checks.
        }
    }

    private static async Task DrainAsync(params Task<string>[] tasks)
    {
        try
        {
            await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Output is best-effort after cancellation or timeout.
        }
    }
}
