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

    [Fact]
    public void Retention_ClampsToAtLeastOne()
    {
        var log = new OperationLog(_dir, 0); // 0 应被钳制为 1
        for (var i = 0; i < 3; i++)
            log.Save(new OperationRecord { Timestamp = DateTime.Now.AddMinutes(i), Entries = { new LogEntry { Source = i.ToString(), Dest = "d" } } });

        Assert.Single(Directory.GetFiles(_dir, "*.json")); // 0 被钳制为 1 份
    }

    [Fact]
    public void Latest_ReadsLegacyPascalCaseLog()
    {
        // 旧版本序列化无选项，属性名为 PascalCase（Timestamp/Entries/Source/Dest）
        File.WriteAllText(Path.Combine(_dir, "op-20260101000000000.json"),
            "{\"Timestamp\":\"2026-01-01T00:00:00\",\"Entries\":[{\"Source\":\"C:\\\\a.txt\",\"Dest\":\"C:\\\\b.txt\"}]}");

        var log = new OperationLog(_dir, 10);
        var latest = log.Latest();

        Assert.NotNull(latest);
        Assert.Single(latest!.Entries);
        Assert.Equal("C:\\b.txt", latest.Entries[0].Dest);
    }
}