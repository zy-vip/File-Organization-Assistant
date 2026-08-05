// tests/FileTidy.Tests/OperationLogTests.cs
using System.IO;
using FileTidy.Core;

namespace FileTidy.Tests;

public class OperationLogTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("oplog").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void Latest_ReturnsMostRecentRecord()
    {
        var log = new OperationLog(_dir, 10);
        log.Save(new OperationRecord { Timestamp = DateTime.Now, Entries = { new LogEntry { Source = "a", Dest = "b" } } });
        log.Save(new OperationRecord { Timestamp = DateTime.Now.AddSeconds(1), Entries = { new LogEntry { Source = "c", Dest = "d" } } });

        var latest = log.Latest();
        Assert.NotNull(latest);
        Assert.Equal("c", latest.Entries[0].Source);
    }

    [Fact]
    public void DiscardLatest_RemovesMostRecent()
    {
        var log = new OperationLog(_dir, 10);
        log.Save(new OperationRecord { Timestamp = DateTime.Now, Entries = { new LogEntry { Source = "a", Dest = "b" } } });
        log.Save(new OperationRecord { Timestamp = DateTime.Now.AddSeconds(1), Entries = { new LogEntry { Source = "c", Dest = "d" } } });

        log.DiscardLatest();
        var latest = log.Latest();
        Assert.Equal("a", latest!.Entries[0].Source);
        Assert.Single(Directory.GetFiles(_dir, "*.json")); // 只剩一份日志
    }

    [Fact]
    public void Retention_KeepsOnlyNewest()
    {
        var log = new OperationLog(_dir, 2);
        for (var i = 0; i < 5; i++)
            log.Save(new OperationRecord { Timestamp = DateTime.Now.AddMinutes(i), Entries = { new LogEntry { Source = i.ToString(), Dest = "d" } } });

        Assert.Equal(2, Directory.GetFiles(_dir, "*.json").Length);
    }

    [Fact]
    public void Latest_EmptyDirReturnsNull()
    {
        Assert.Null(new OperationLog(_dir, 10).Latest());
    }
}