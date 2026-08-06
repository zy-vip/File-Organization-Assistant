using System.Windows;
using System.Windows.Input;
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

    // 规则列表拖拽排序：在 ListBox 的 PreviewMouseLeftButtonDown 记录起点，MouseMove 开始拖动，Drop 完成重排
    private Point _dragStart;
    private RuleEditorViewModel? _dragged;

    private void RuleList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragged = (sender as System.Windows.Controls.ListBox)?.SelectedItem as RuleEditorViewModel;
    }

    private void RuleList_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragged is null || e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStart.X) < 10 && Math.Abs(pos.Y - _dragStart.Y) < 10) return;
        if (sender is System.Windows.Controls.ListBox lb)
            DragDrop.DoDragDrop(lb, _dragged, DragDropEffects.Move);
    }

    private void RuleList_Drop(object sender, DragEventArgs e)
    {
        if (sender is not System.Windows.Controls.ListBox lb || _dragged is null || e.Data.GetData(typeof(RuleEditorViewModel)) is not RuleEditorViewModel target) return;
        if (DataContext is not MainViewModel vm) return;
        var from = vm.EditorVms.IndexOf(_dragged);
        var to = vm.EditorVms.IndexOf(target);
        if (from >= 0 && to >= 0 && from != to) vm.MoveRule(from, to - from);
        _dragged = null;
    }
}