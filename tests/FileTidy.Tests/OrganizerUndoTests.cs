// tests/FileTidy.Tests/OrganizerUndoTests.cs
using System.IO;
using FileTidy.Core;
using FileTidy.Core.Models;

namespace FileTidy.Tests;

public class OrganizerUndoTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("undo").FullName;
    public void Dispose() => Directory.Delete(_root, true);

    private static PreviewEntry MovedEntry(string src, string dest)
        => new()
        {
            File = new FileEntry { FullPath = src, FileName = Path.GetFileName(src),
                                   Extension = Path.GetExtension(src).TrimStart('.').ToLowerInvariant(),
                                   LastWriteTime = DateTime.Now },
            MatchedRule = new Rule
            {
                TargetPath = Path.GetDirectoryName(dest)!,
                Conditions = { new ExtensionCondition { Extensions = { Path.GetExtension(src).TrimStart('.').ToLowerInvariant() } } }
            },
            DestPath = dest,
            Status = PreviewStatus.Moved
        };

    [Fact]
    public void Undo_MovesFilesBack()
    {
        var src = Path.Combine(_root, "a.txt");
        var dest = Path.Combine(_root, "out", "a.txt");
        File.WriteAllText(src, "x");
        Organizer.Execute(new List<PreviewEntry> { MovedEntry(src, dest) }, DateTime.Now);

        var record = new OperationRecord
        {
            Timestamp = DateTime.Now,
            Entries = { new LogEntry { Source = src, Dest = dest } }
        };
        var result = Organizer.Undo(record);

        Assert.True(File.Exists(src));
        Assert.False(File.Exists(dest));
        Assert.Equal(1, result.Restored);
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public void Undo_MissingFileIsSkipped()
    {
        var record = new OperationRecord
        {
            Timestamp = DateTime.Now,
            Entries = { new LogEntry { Source = Path.Combine(_root, "a.txt"), Dest = Path.Combine(_root, "b.txt") } }
        };
        var result = Organizer.Undo(record);
        Assert.Equal(0, result.Restored);
        Assert.Single(result.Skipped);
    }

    [Fact]
    public void Undo_AcrossRestart_UsesPersistedLog()
    {
        // 模拟：整理落盘 → "重启"（新 OperationLog 实例）→ 读日志 → 撤销
        var src = Path.Combine(_root, "a.txt");
        var dest = Path.Combine(_root, "out", "a.txt");
        File.WriteAllText(src, "x");
        var logDir = Path.Combine(_root, "logs");

        var log1 = new OperationLog(logDir, 10);
        var (_, record) = Organizer.Execute(new List<PreviewEntry> { MovedEntry(src, dest) }, DateTime.Now);
        log1.Save(record);

        var log2 = new OperationLog(logDir, 10); // 新实例 = 跨重启
        var loaded = log2.Latest();
        Assert.NotNull(loaded);

        var result = Organizer.Undo(loaded!);
        Assert.Equal(1, result.Restored);
        Assert.True(File.Exists(src));
        Assert.False(File.Exists(dest));
    }
}