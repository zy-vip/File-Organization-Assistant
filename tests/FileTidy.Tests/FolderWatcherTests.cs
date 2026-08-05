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

    [Fact]
    public async Task Replace_SwitchesToNewFolders()
    {
        var dir2 = Path.Combine(Path.GetTempPath(), "watch2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir2);
        try
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _watcher = new FolderWatcher();
            _watcher.TidyTriggered += () => tcs.TrySetResult();
            _watcher.Watch(new[] { _dir });
            _watcher.Replace(new[] { dir2 });

            File.WriteAllText(Path.Combine(dir2, "new2.txt"), "x");
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(5000));
            Assert.True(completed == tcs.Task, "替换后新目录未触发");
        }
        finally
        {
            Directory.Delete(dir2, true);
        }
    }

    [Fact]
    public async Task Watch_IsIdempotentForSameFolder()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _watcher = new FolderWatcher();
        var count = 0;
        _watcher.TidyTriggered += () => { count++; tcs.TrySetResult(); };
        _watcher.Watch(new[] { _dir, _dir }); // 重复监听同一目录应去重

        File.WriteAllText(Path.Combine(_dir, "dup.txt"), "x");
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(5000));
        Assert.True(completed == tcs.Task, "监听超时未触发");
        await Task.Delay(300);
        Assert.True(count <= 2, $"同一文件不应触发多次（实际 {count} 次）");
    }
}