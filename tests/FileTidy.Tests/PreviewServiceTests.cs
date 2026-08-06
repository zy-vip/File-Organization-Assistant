// tests/FileTidy.Tests/PreviewServiceTests.cs
using System.IO;
using FileTidy.Core;
using FileTidy.Core.Models;

namespace FileTidy.Tests;

public class PreviewServiceTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("preview").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    private static FileEntry MakeFile(string name)
        => new()
        {
            FullPath = Path.Combine(@"C:\tmp", name),
            FileName = name,
            Extension = Path.GetExtension(name).TrimStart('.').ToLowerInvariant(),
            LastWriteTime = DateTime.Now
        };

    [Fact]
    public void Build_MatchedFileGetsDestPath()
    {
        var rules = new List<Rule>
        {
            new() { Name = "图", SourcePath = @"C:\tmp", TargetPath = @"C:\pics",
                    Conditions = { new ExtensionCondition { Extensions = { "jpg" } } } }
        };
        var previews = PreviewService.Build(rules, new[] { MakeFile("a.jpg"), MakeFile("b.txt") }, DateTime.Now);
        Assert.Equal(2, previews.Count);
        Assert.Equal(PreviewStatus.Moved, previews[0].Status);
        Assert.Equal(@"C:\pics\a.jpg", previews[0].DestPath);
        Assert.Equal(PreviewStatus.NoMatch, previews[1].Status);
        Assert.Null(previews[1].DestPath);
    }

    [Fact]
    public void Build_NoDoubleProcessingAcrossRules()
    {
        var rules = new List<Rule>
        {
            new() { Name = "A", SourcePath = @"C:\tmp", TargetPath = @"C:\a",
                    Conditions = { new ExtensionCondition { Extensions = { "exe" } } } },
            new() { Name = "B", SourcePath = @"C:\tmp", TargetPath = @"C:\b",
                    Conditions = { new KeywordCondition { Keyword = "install" } } }
        };
        var previews = PreviewService.Build(rules, new[] { MakeFile("installer.exe") }, DateTime.Now);
        Assert.Single(previews);
        Assert.Equal(@"C:\a\installer.exe", previews[0].DestPath);
    }

    [Fact]
    public void Build_AutoRenamesOnDestConflict()
    {
        var target = Path.Combine(_dir, "target");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "a.jpg"), "existing");

        var rules = new List<Rule>
        {
            new() { Name = "图", SourcePath = _dir, TargetPath = target,
                    Conditions = { new ExtensionCondition { Extensions = { "jpg" } } } }
        };
        var srcFile = Path.Combine(_dir, "a.jpg");
        File.WriteAllText(srcFile, "new");

        var file = new FileEntry { FullPath = srcFile, FileName = "a.jpg", Extension = "jpg", LastWriteTime = DateTime.Now };
        var previews = PreviewService.Build(rules, new[] { file }, DateTime.Now);

        Assert.Equal(PreviewStatus.Moved, previews[0].Status);
        Assert.Equal(Path.Combine(target, "a(1).jpg"), previews[0].DestPath);
    }

    [Fact]
    public void Build_ConflictStatus_WhenNoAutoRename()
    {
        // 未启用自动序号时目标已存在 → Conflict 状态，DestPath 保持原冲突路径
        var target = Path.Combine(_dir, "target");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "a.jpg"), "existing");

        var rules = new List<Rule>
        {
            new() { Name = "图", SourcePath = _dir, TargetPath = target, AutoRenameOnConflict = false,
                    Conditions = { new ExtensionCondition { Extensions = { "jpg" } } } }
        };
        var srcFile = Path.Combine(_dir, "a.jpg");
        File.WriteAllText(srcFile, "new");

        var file = new FileEntry { FullPath = srcFile, FileName = "a.jpg", Extension = "jpg", LastWriteTime = DateTime.Now };
        var previews = PreviewService.Build(rules, new[] { file }, DateTime.Now);

        Assert.Equal(PreviewStatus.Conflict, previews[0].Status);
        Assert.Equal(Path.Combine(target, "a.jpg"), previews[0].DestPath);
    }

    private static readonly DateTime Now = new(2026, 8, 6, 12, 0, 0);

    [Fact]
    public void Build_RenameActionRendersDestName()
    {
        var rules = new List<Rule>
        {
            new()
            {
                Name = "重命名", SourcePath = @"C:\tmp", TargetPath = @"C:\out",
                Conditions = { new RegexCondition { Pattern = @"(\d{4})\.jpg$" } },
                Actions = { new MoveAndRenameAction { Template = "{1}_{date:yyyyMMdd}{ext}" } }
            }
        };
        var previews = PreviewService.Build(rules, new[] { FileEntry("a2026.jpg"), FileEntry("b2025.jpg") }, Now);
        Assert.Equal(PreviewStatus.Moved, previews[0].Status);
        Assert.Equal(@"C:\out\2026_20260806.jpg", previews[0].DestPath);
        Assert.Equal(@"C:\out\2025_20260806.jpg", previews[1].DestPath);
    }

    [Fact]
    public void Build_SequencePerRuleRestarts()
    {
        var r1 = new Rule { SourcePath = @"C:\a", TargetPath = @"C:\out",
                            Conditions = { new ExtensionCondition { Extensions = { "jpg" } } },
                            Actions = { new MoveAndRenameAction { Template = "图{n}{ext}" } } };
        var r2 = new Rule { SourcePath = @"C:\a", TargetPath = @"C:\out2",
                            Conditions = { new ExtensionCondition { Extensions = { "png" } } },
                            Actions = { new MoveAndRenameAction { Template = "图{n}{ext}" } } };
        var previews = PreviewService.Build(new List<Rule> { r1, r2 },
            new[] { FileEntry("a.jpg"), FileEntry("b.jpg"), FileEntry("c.png") }, Now);
        Assert.Equal(@"C:\out\图1.jpg", previews[0].DestPath);
        Assert.Equal(@"C:\out\图2.jpg", previews[1].DestPath);
        Assert.Equal(@"C:\out2\图1.png", previews[2].DestPath);
    }

    [Fact]
    public void Build_TemplateErrorOnBadTemplate()
    {
        var rules = new List<Rule>
        {
            new() { SourcePath = @"C:\tmp", TargetPath = @"C:\out",
                    Conditions = { new ExtensionCondition { Extensions = { "jpg" } } },
                    Actions = { new MoveAndRenameAction { Template = "{1}{ext}" } } } // 无正则条件 → 捕获组缺失
        };
        var previews = PreviewService.Build(rules, new[] { FileEntry("a.jpg") }, Now);
        Assert.Equal(PreviewStatus.TemplateError, previews[0].Status);
        Assert.Null(previews[0].DestPath);
    }

    [Fact]
    public void Build_NeedsProWhenNotAllowed()
    {
        var rules = new List<Rule>
        {
            new() { SourcePath = @"C:\tmp", TargetPath = @"C:\out",
                    Conditions = { new RegexCondition { Pattern = @"jpg" } } }
        };
        var previews = PreviewService.Build(rules, new[] { FileEntry("a.jpg") }, Now, f => f != ProFeature.RegularExpression);
        Assert.Equal(PreviewStatus.NeedsPro, previews[0].Status);
        Assert.Null(previews[0].DestPath);
        Assert.Equal("正则条件", previews[0].BlockedFeature);
    }

    [Fact]
    public void Build_NeedsProJoinsMultipleFeatures()
    {
        var rules = new List<Rule>
        {
            new() { SourcePath = @"C:\tmp", TargetPath = @"C:\out",
                    Conditions = { new RegexCondition { Pattern = @"jpg" } },
                    Actions = { new MoveAndRenameAction { Template = "{1}{ext}" } } }
        };
        var previews = PreviewService.Build(rules, new[] { FileEntry("a.jpg") }, Now, _ => false);
        Assert.Equal(PreviewStatus.NeedsPro, previews[0].Status);
        Assert.Equal("正则条件 / 重命名模板", previews[0].BlockedFeature);
    }

    private static FileEntry FileEntry(string name) => new()
    {
        FullPath = Path.Combine(@"C:\tmp", name),
        FileName = name,
        Extension = Path.GetExtension(name).TrimStart('.').ToLowerInvariant(),
        LastWriteTime = Now
    };
}