// tests/FileTidy.Tests/FolderWatcherTests.cs
using System.IO;
using FileTidy.Core;

namespace FileTidy.Tests;

public class FolderWatcherTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("watch").FullName;
    private FolderWatcher? _watcher;
    public void Dispose() { _watcher?.Dispose(); Directory.Delete(_dir, true); }

    [Fact]
    public async Task Watch_CreatedFileRaisesTidyTriggered()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _watcher = new FolderWatcher();
        _watcher.TidyTriggered += () => tcs.TrySetResult();
        _watcher.Watch(new[] { _dir });

        File.WriteAllText(Path.Combine(_dir, "new.txt"), "x");
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(5000));
        Assert.True(completed == tcs.Task, "监听超时未触发");
    }
}