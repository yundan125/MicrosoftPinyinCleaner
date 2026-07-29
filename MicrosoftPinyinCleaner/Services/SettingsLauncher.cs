using System.Diagnostics;
using MicrosoftPinyinCleaner.Models;

namespace MicrosoftPinyinCleaner.Services;

public sealed class SettingsLauncher
{
    public OperationResult OpenLanguageSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:regionlanguage",
                UseShellExecute = true
            });
            return OperationResult.Ok("已打开 Windows 语言和区域设置。");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"无法打开语言设置：{ex.Message}");
        }
    }
}
