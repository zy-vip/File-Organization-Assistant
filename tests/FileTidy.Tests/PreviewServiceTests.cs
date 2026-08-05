// tests/FileTidy.Tests/PreviewServiceTests.cs
using System.IO;
using FileTidy.Core;
using FileTidy.Core.Models;

namespace FileTidy.Tests;

public class PreviewServiceTests
{
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
        var dir = Directory.CreateTempSubdirectory("preview").FullName;
        var target = Path.Combine(dir, "target");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "a.jpg"), "existing");

        var rules = new List<Rule>
        {
            new() { Name = "图", SourcePath = dir, TargetPath = target,
                    Conditions = { new ExtensionCondition { Extensions = { "jpg" } } } }
        };
        var srcFile = Path.Combine(dir, "a.jpg");
        File.WriteAllText(srcFile, "new");

        var file = new FileEntry { FullPath = srcFile, FileName = "a.jpg", Extension = "jpg", LastWriteTime = DateTime.Now };
        var previews = PreviewService.Build(rules, new[] { file }, DateTime.Now);

        Assert.Equal(PreviewStatus.Moved, previews[0].Status);
        Assert.Equal(Path.Combine(target, "a(1).jpg"), previews[0].DestPath);
    }
}