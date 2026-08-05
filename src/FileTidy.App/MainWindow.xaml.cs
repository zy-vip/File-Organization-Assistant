using System.Windows;
using FileTidy.App.ViewModels;

namespace FileTidy.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    /// <summary>关闭窗口 = 隐藏到托盘；仅当应用正在退出（托盘"退出"菜单）时才真正关闭</summary>
    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (App.IsExiting) return;
        e.Cancel = true;
        Hide();
        (DataContext as MainViewModel)?.Shutdown();
    }
}