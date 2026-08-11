using System.Threading;

namespace FileTidy.App;

/// <summary>单实例守护：以命名互斥体保证同一用户会话内只有一个实例；非首例 Dispose 时不持有互斥体</summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex? _mutex;

    /// <summary>当前进程是否为第一个实例</summary>
    public bool IsFirstInstance { get; }

    public SingleInstanceGuard(string name)
    {
        _mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        IsFirstInstance = createdNew;
        if (!IsFirstInstance)
        {
            _mutex.Dispose();
            _mutex = null;
        }
    }

    public void Dispose() => _mutex?.Dispose();
}