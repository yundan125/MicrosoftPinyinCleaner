using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using MicrosoftPinyinCleaner.Models;
using MicrosoftPinyinCleaner.Services;

namespace MicrosoftPinyinCleaner;

public partial class MainWindow : Window
{
    private static readonly SolidColorBrush GreenBrush = CreateFrozenBrush("#46C987");
    private static readonly SolidColorBrush RedBrush = CreateFrozenBrush("#F06A73");
    private static readonly SolidColorBrush AmberBrush = CreateFrozenBrush("#E8AA4C");
    private static readonly SolidColorBrush GrayBrush = CreateFrozenBrush("#7E8999");

    private readonly InputMethodService _inputMethodService = new();
    private readonly StartupService _startupService = new();
    private readonly SettingsLauncher _settingsLauncher = new();
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private bool _isBusy;
    private bool _hasMicrosoftPinyin;
    private StartupStatus _startupStatus = new(false, false, string.Empty);

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => EnableImmersiveDarkTitleBar();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshStartupStatus();
        await RefreshDetectionAsync(updateLastResult: true);
    }

    private async void DetectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        await RefreshDetectionAsync(updateLastResult: true);
    }

    private async void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        SetBusy(true, "正在处理…");
        try
        {
            var result = await _inputMethodService.RemoveMicrosoftPinyinAsync(_lifetimeCancellation.Token);
            LastResultText.Text = result.Message;
            LastResultText.Foreground = result.Outcome switch
            {
                RemovalOutcome.Removed or RemovalOutcome.NotInstalled => GreenBrush,
                RemovalOutcome.BlockedNoAlternative => AmberBrush,
                _ => RedBrush
            };

            if (result.Outcome == RemovalOutcome.BlockedNoAlternative)
            {
                MessageBox.Show(
                    this,
                    result.Message,
                    "已阻止不安全的删除",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            await RefreshDetectionCoreAsync(updateLastResult: false);
        }
        catch (OperationCanceledException)
        {
            // Window is closing.
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void EnableStartupButton_Click(object sender, RoutedEventArgs e)
    {
        await RunStartupOperationAsync(_startupService.Enable);
    }

    private async void DisableStartupButton_Click(object sender, RoutedEventArgs e)
    {
        await RunStartupOperationAsync(_startupService.Disable);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        var result = _settingsLauncher.OpenLanguageSettings();
        ShowOperationResult(result);
    }

    private async Task RefreshDetectionAsync(bool updateLastResult)
    {
        SetBusy(true, "正在检测…");
        try
        {
            await RefreshDetectionCoreAsync(updateLastResult);
        }
        catch (OperationCanceledException)
        {
            // Window is closing.
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RefreshDetectionCoreAsync(bool updateLastResult)
    {
        var result = await _inputMethodService.DetectAsync(_lifetimeCancellation.Token);
        _hasMicrosoftPinyin = result.State == MicrosoftPinyinState.Installed;

        switch (result.State)
        {
            case MicrosoftPinyinState.Installed:
                PinyinStatusText.Text = "已安装";
                PinyinStatusText.Foreground = AmberBrush;
                PinyinStatusDot.Fill = AmberBrush;
                break;
            case MicrosoftPinyinState.NotInstalled:
                PinyinStatusText.Text = "未安装";
                PinyinStatusText.Foreground = GreenBrush;
                PinyinStatusDot.Fill = GreenBrush;
                break;
            default:
                PinyinStatusText.Text = "检测失败";
                PinyinStatusText.Foreground = RedBrush;
                PinyinStatusDot.Fill = RedBrush;
                break;
        }

        if (updateLastResult)
        {
            LastResultText.Text = result.Message;
            LastResultText.Foreground = result.Success ? (result.State == MicrosoftPinyinState.Installed ? AmberBrush : GreenBrush) : RedBrush;
        }
    }

    private async Task RunStartupOperationAsync(Func<OperationResult> operation)
    {
        if (_isBusy)
        {
            return;
        }

        SetBusy(true, "正在处理…");
        try
        {
            var result = await Task.Run(operation, _lifetimeCancellation.Token);
            ShowOperationResult(result);
            RefreshStartupStatus();
        }
        catch (OperationCanceledException)
        {
            // Window is closing.
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void RefreshStartupStatus()
    {
        _startupStatus = _startupService.GetStatus();
        StartupStatusText.Text = _startupStatus.IsEnabled ? "已开启" : "未开启";
        StartupStatusText.Foreground = _startupStatus.IsEnabled ? GreenBrush : (_startupStatus.HasStaleEntry ? AmberBrush : GrayBrush);
        StartupStatusDot.Fill = StartupStatusText.Foreground;

        if (_startupStatus.HasStaleEntry)
        {
            LastResultText.Text = _startupStatus.Message;
            LastResultText.Foreground = AmberBrush;
        }
    }

    private void ShowOperationResult(OperationResult result)
    {
        LastResultText.Text = result.Message;
        LastResultText.Foreground = result.Success ? GreenBrush : RedBrush;
    }

    private void SetBusy(bool isBusy, string? statusText = null)
    {
        _isBusy = isBusy;
        DetectButton.IsEnabled = !isBusy;
        RemoveButton.IsEnabled = !isBusy && _hasMicrosoftPinyin;
        SettingsButton.IsEnabled = !isBusy;
        EnableStartupButton.IsEnabled = !isBusy && !_startupStatus.IsEnabled;
        DisableStartupButton.IsEnabled = !isBusy && (_startupStatus.IsEnabled || _startupStatus.HasStaleEntry);
        Cursor = isBusy ? System.Windows.Input.Cursors.Wait : null;

        if (!string.IsNullOrWhiteSpace(statusText))
        {
            LastResultText.Text = statusText;
            LastResultText.Foreground = GrayBrush;
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _lifetimeCancellation.Cancel();
        _lifetimeCancellation.Dispose();
    }

    private void EnableImmersiveDarkTitleBar()
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            var enabled = 1;
            if (DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(handle, 19, ref enabled, sizeof(int));
            }
        }
        catch
        {
            // Older Windows builds can use the default title bar safely.
        }
    }

    private static SolidColorBrush CreateFrozenBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr windowHandle, int attribute, ref int value, int valueSize);
}
