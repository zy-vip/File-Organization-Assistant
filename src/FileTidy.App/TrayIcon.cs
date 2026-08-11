using System.Windows;
using System.Windows.Controls;
using FileTidy.App.ViewModels;
using Hardcodet.Wpf.TaskbarNotification;

namespace FileTidy.App;

/// <summary>系统托盘：双击打开主窗口，右键菜单含立即整理与退出；自动整理完成时弹通知</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly TaskbarIcon _icon;
    private readonly MainViewModel _vm;

    public TrayIcon(MainViewModel vm)
    {
        _vm = vm;
        _icon = new TaskbarIcon
        {
            Icon = LoadIcon(),
            ToolTipText = "文件整理助手",
            ContextMenu = BuildMenu()
        };
        _icon.TrayMouseDoubleClick += (_, _) => ShowWindow();
        // 自动整理完成 → 托盘通知（事件来自线程池，异步调度回 UI 线程）
        vm.TidyCompleted += msg => Application.Current.Dispatcher.BeginInvoke(() =>
            _icon.ShowBalloonTip("文件整理助手", msg, BalloonIcon.Info));
    }

    /// <summary>从当前可执行文件提取图标；失败时返回 null（不阻塞启动）</summary>
    private static System.Drawing.Icon? LoadIcon()
    {
        try
        {
            return System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "");
        }
        catch
        {
            return null;
        }
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();
        var open = new MenuItem { Header = "打开主界面" };
        open.Click += (_, _) => ShowWindow();
        var tidy = new MenuItem { Header = "立即整理" };
        tidy.Click += async (_, _) => await _vm.TidyCommand.ExecuteAsync();
        var quit = new MenuItem { Header = "退出" };
        quit.Click += (_, _) => App.ExitApp();
        menu.Items.Add(open); menu.Items.Add(tidy); menu.Items.Add(quit);
        return menu;
    }

    private void ShowWindow()
    {
        var window = Application.Current.MainWindow;
        if (window is null) return;
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
    }

    public void Dispose() => _icon.Dispose();
}