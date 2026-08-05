using System.Windows;
using FileTidy.App.ViewModels;
using FileTidy.Core;

namespace FileTidy.App;

public partial class App : Application
{
    private TrayIcon? _tray;
    private MainViewModel? _vm;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
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