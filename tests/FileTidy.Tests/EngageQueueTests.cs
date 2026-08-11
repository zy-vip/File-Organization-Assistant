// tests/FileTidy.Tests/EngageQueueTests.cs
using System.Threading;
using FileTidy.Core;

namespace FileTidy.Tests;

public class EngageQueueTests
{
    [Fact]
    public async Task RunAsync_ExecutesWhenIdle()
    {
        var q = new EngageQueue();
        var result = await q.RunAsync(async () => { await Task.Delay(10); return 42; });
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task RunAsync_Busy_ThrowsBusyException()
    {
        var q = new EngageQueue();
        var first = q.RunAsync(async () => { await Task.Delay(200); return 1; });
        await Assert.ThrowsAsync<BusyException>(() => q.RunAsync(() => Task.FromResult(2)));
        await first;
    }

    [Fact]
    public async Task RunAsync_Concurrent_OnlyOneSucceeds()
    {
        // 8 个并发调用恰 1 个成功；门控 TCS 保证忙窗口，轮询计数保证全部已尝试
        var q = new EngageQueue();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        var succeeded = 0;
        var rejected = 0;

        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            Interlocked.Increment(ref attempts);
            try
            {
                await q.RunAsync(async () => { await gate.Task; return 1; });
                Interlocked.Increment(ref succeeded);
            }
            catch (BusyException)
            {
                Interlocked.Increment(ref rejected);
            }
        })).ToArray();

        // 等待全部任务完成"进入或被拒"后再释放门，杜绝时序竞态
        while (Volatile.Read(ref attempts) < 8) await Task.Delay(10);
        gate.SetResult();
        await Task.WhenAll(tasks);

        Assert.Equal(1, succeeded);
        Assert.Equal(7, rejected);

        // 队列恢复空闲，可再次执行
        Assert.Equal(2, await q.RunAsync(() => Task.FromResult(2)));
    }
}