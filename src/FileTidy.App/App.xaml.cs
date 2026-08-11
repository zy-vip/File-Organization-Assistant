using System.Windows;
using FileTidy.App.ViewModels;
using FileTidy.Core;

namespace FileTidy.App;

public partial class App : Application
{
    private TrayIcon? _tray;
    private MainViewModel? _vm;
    private SingleInstanceGuard? _guard;

    /// <summary>应用是否正在退出（托盘"退出"触发；窗口关闭仅隐藏，不退出进程）</summary>
    public static bool IsExiting { get; private set; }

    /// <summary>正式的退出入口：置标志后关闭应用，让窗口 Closing 放行</summary>
    public static void ExitApp()
    {
        IsExiting = true;
        Current.Shutdown();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _guard = new SingleInstanceGuard("FileTidy.SingleInstance");
        if (!_guard.IsFirstInstance)
        {
            MessageBox.Show("FileTidy 已在运行，请从系统托盘打开主界面。", "文件整理助手",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }
        AppPaths.EnsureCreated();
        _vm = new MainViewModel(new SettingsService(AppPaths.ConfigFile));

        var window = new MainWindow { DataContext = _vm };
        MainWindow = window;
        _tray = new TrayIcon(_vm);
        // 带命令行参数启动时（如开机自启）不显示主窗口，只驻留托盘
        if (e.Args.Length == 0) window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _vm?.Shutdown();
        _tray?.Dispose();
        base.OnExit(e);
    }
}