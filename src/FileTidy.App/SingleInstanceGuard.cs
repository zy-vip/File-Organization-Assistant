using System.Threading;

namespace FileTidy.App;

/// <summary>单实例守护：以命名互斥体保证同一用户会话内只有一个实例；非首例 Dispose 时不持有互斥体。
/// 注意：未加 Global\ 前缀，互斥体按会话隔离（RDP/快速用户切换下同一用户多会话可各起一个实例，会共享 config/operations 目录）。
/// 桌面工具按会话隔离可接受，属已知边界</summary>
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