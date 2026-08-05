using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace FileTidy.App.ViewModels;

/// <summary>INotifyPropertyChanged 基类 + 轻量命令</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>设置属性并触发通知（可重写以扩展副作用，如即时校验）</summary>
    protected virtual bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>异步命令：Execute 为 async void 兼容 ICommand；ExecuteAsync 返回 Task 便于测试</summary>
public sealed class RelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    public RelayCommand(Func<Task> execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public async void Execute(object? parameter) => await ExecuteAsync();
    public Task ExecuteAsync() => _execute();
}