using System.Windows;
using MicrosoftPinyinCleaner.Models;
using MicrosoftPinyinCleaner.Services;

namespace MicrosoftPinyinCleaner;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Any(argument => string.Equals(argument, "--silent-clean", StringComparison.OrdinalIgnoreCase)))
        {
            await RunSilentCleanupAsync();
            return;
        }

        ShutdownMode = ShutdownMode.OnMainWindowClose;
        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    private async Task RunSilentCleanupAsync()
    {
        var logger = new AppLogger();

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            var service = new InputMethodService();
            var result = await service.RemoveMicrosoftPinyinAsync();
            var logResult = result.Outcome switch
            {
                RemovalOutcome.Removed => "已安全移除微软拼音。",
                RemovalOutcome.NotInstalled => "未检测到微软拼音，无需处理。",
                RemovalOutcome.BlockedNoAlternative => "缺少其他中文输入法，未执行删除。",
                _ => "清理失败，未确认输入法列表已更改。"
            };
            await logger.WriteLatestAsync("登录静默清理", logResult);
        }
        catch
        {
            await logger.WriteLatestAsync("登录静默清理", "发生未处理异常，清理已终止。");
        }
        finally
        {
            Shutdown(0);
        }
    }
}
