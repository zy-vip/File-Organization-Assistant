// tests/FileTidy.Tests/EngageQueueTests.cs
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
    public async Task RunAsync_RejectsWhenBusy()
    {
        var q = new EngageQueue();
        var first = q.RunAsync(async () => { await Task.Delay(200); return 1; });
        await Assert.ThrowsAsync<InvalidOperationException>(() => q.RunAsync(() => Task.FromResult(2)));
        await first;
    }
}