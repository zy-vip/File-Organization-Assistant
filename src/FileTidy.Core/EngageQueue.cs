namespace FileTidy.Core;

/// <summary>队列忙拒绝异常：与整理逻辑内部异常区分开，调用方按类型分流（忙→重试，真实失败→报告）</summary>
public sealed class BusyException : InvalidOperationException
{
    public BusyException(string message) : base(message) { }
}

/// <summary>整理互斥队列：手动/自动任务串行执行，忙时拒绝新任务</summary>
public sealed class EngageQueue
{
    private bool _busy;
    private readonly object _lock = new();

    public bool IsBusy { get { lock (_lock) return _busy; } }

    /// <summary>执行任务；空闲时立即执行并置忙，忙时抛 BusyException</summary>
    public async Task<T> RunAsync<T>(Func<Task<T>> work)
    {
        lock (_lock)
        {
            if (_busy) throw new BusyException("整理正在进行中");
            _busy = true;
        }
        try
        {
            return await work();
        }
        finally
        {
            lock (_lock) _busy = false;
        }
    }
}