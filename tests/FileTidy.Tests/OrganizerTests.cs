// tests/FileTidy.Tests/OrganizerTests.cs
using System.Diagnostics;
using System.IO;
using FileTidy.Core;
using FileTidy.Core.Models;

namespace FileTidy.Tests;

public class OrganizerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("org").FullName;
    public void Dispose() => Directory.Delete(_root, true);

    private string Write(string relative)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        return path;
    }

    private static PreviewEntry Moved(string fullPath, string dest)
        => new()
        {
            File = new FileEntry { FullPath = fullPath, FileName = Path.GetFileName(fullPath),
                                   Extension = Path.GetExtension(fullPath).TrimStart('.').ToLowerInvariant(),
                                   LastWriteTime = DateTime.Now },
            MatchedRule = new Rule
            {
                TargetPath = Path.GetDirectoryName(dest)!,
                Conditions = { new ExtensionCondition { Extensions = { Path.GetExtension(fullPath).TrimStart('.').ToLowerInvariant() } } }
            },
            DestPath = dest,
            Status = PreviewStatus.Moved
        };

    [Fact]
    public void Execute_MovesFileAndSkipsMissing()
    {
        var src = Write("a.txt");
        var dest = Path.Combine(_root, "out", "a.txt");
        var (result, record) = Organizer.Execute(new List<PreviewEntry>
        {
            Moved(src, dest),
            Moved(Path.Combine(_root, "gone.txt"), Path.Combine(_root, "out", "gone.txt"))
        }, DateTime.Now);

        Assert.True(File.Exists(dest));
        Assert.Equal(1, result.Succeeded);
        Assert.Single(result.Skipped);
        Assert.Equal("文件已不存在", result.Skipped[0].Reason);
        Assert.Single(record.Entries);
        Assert.Equal(src, record.Entries[0].Source);
    }

    [Fact]
    public void Execute_OccupiedFileGoesToFailed()
    {
        var src = Write("locked.txt");
        var dest = Path.Combine(_root, "out", "locked.txt");
        using (var handle = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var (result, _) = Organizer.Execute(new List<PreviewEntry> { Moved(src, dest) }, DateTime.Now);
            Assert.Equal(0, result.Succeeded);
            Assert.Single(result.Failed);
        }
    }

    [Fact]
    public void Execute_CrossVolumeUsesCopyDelete()
    {
        var driveChar = FreeDrive();
        var targetVol = Path.Combine(_root, "vol");
        Directory.CreateDirectory(targetVol);
        var src = Write("a.txt");
        var dest = Path.Combine($@"{driveChar}:\", "a.txt");
        try
        {
            RunSubst(driveChar, targetVol);
            var (result, record) = Organizer.Execute(new List<PreviewEntry> { Moved(src, dest) }, DateTime.Now);

            Assert.Equal(1, result.Succeeded);
            Assert.True(File.Exists(dest));
            Assert.False(File.Exists(src));
            Assert.Single(record.Entries);
        }
        finally
        {
            RunSubst(driveChar, null);
        }
    }

    /// <summary>查找空闲盘符（D-Z），避免与现有盘符冲突</summary>
    private static char FreeDrive()
    {
        var used = DriveInfo.GetDrives().Select(d => d.Name[0]).ToHashSet();
        for (var c = 'D'; c <= 'Z'; c++)
            if (!used.Contains(c)) return c;
        throw new Exception("无空闲盘符，无法模拟跨卷移动");
    }

    /// <summary>subst 映射/取消映射虚拟盘符：path 为 null 时执行 subst X: /D</summary>
    private static void RunSubst(char drive, string? path)
    {
        var psi = new ProcessStartInfo("subst")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            Arguments = path is null ? $"{drive}: /D" : $"{drive}: \"{path}\""
        };
        Process.Start(psi)!.WaitForExit();
    }

    [Fact]
    public void Execute_AutoRenameOnConflict_WhenDestTakenAtRunTime()
    {
        // 预览时目标空闲；执行前目标被占用 → 执行期二次唯一化落到 a(1).txt
        var src = Write("a.txt");
        var dest = Path.Combine(_root, "out", "a.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.WriteAllText(dest, "occupied");

        var (result, record) = Organizer.Execute(new List<PreviewEntry> { Moved(src, dest) }, DateTime.Now);

        Assert.Equal(1, result.Succeeded);
        Assert.True(File.Exists(Path.Combine(_root, "out", "a(1).txt")));
        Assert.False(File.Exists(src));
        Assert.Equal(Path.Combine(_root, "out", "a(1).txt"), record.Entries[0].Dest);
    }

    [Fact]
    public void Execute_NoLongerMatchesRule_GoesToSkipped()
    {
        // 预览于 6 天前进行（文件年龄 11 天 ≥ 10 命中）；执行时年龄 5 天 < 10 → 再校验失败
        var src = Write("old.txt");
        var now = new DateTime(2026, 8, 6, 12, 0, 0);
        var entry = new PreviewEntry
        {
            File = new FileEntry { FullPath = src, FileName = "old.txt", Extension = "txt", LastWriteTime = now.AddDays(-5) },
            MatchedRule = new Rule
            {
                TargetPath = Path.Combine(_root, "out"),
                Conditions = { new AgeCondition { Days = 10 } }
            },
            DestPath = Path.Combine(_root, "out", "old.txt"),
            Status = PreviewStatus.Moved
        };

        var (result, _) = Organizer.Execute(new List<PreviewEntry> { entry }, now);

        Assert.Equal(0, result.Succeeded);
        Assert.Single(result.Skipped);
        Assert.Equal("不再匹配规则", result.Skipped[0].Reason);
        Assert.True(File.Exists(src));
    }
}