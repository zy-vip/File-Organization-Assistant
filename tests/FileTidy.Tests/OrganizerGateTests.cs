// tests/FileTidy.Tests/OrganizerGateTests.cs
using FileTidy.Core;
using FileTidy.Core.Models;

namespace FileTidy.Tests;

public class OrganizerGateTests
{
    [Fact]
    public void Execute_NeedsProGoesToSkipped()
    {
        var p = new PreviewEntry
        {
            File = new FileEntry { FullPath = @"C:\x\a.jpg", FileName = "a.jpg", Extension = "jpg", LastWriteTime = DateTime.Now },
            MatchedRule = new Rule { Conditions = { new RegexCondition { Pattern = "jpg" } } },
            Status = PreviewStatus.NeedsPro
        };
        var (result, _) = Organizer.Execute(new List<PreviewEntry> { p }, DateTime.Now);
        Assert.Equal(0, result.Succeeded);
        Assert.Single(result.Skipped);
        Assert.Contains("Pro", result.Skipped[0].Reason);
        Assert.Empty(result.Failed);
    }

    [Fact]
    public void Execute_TemplateErrorGoesToFailed()
    {
        var p = new PreviewEntry
        {
            File = new FileEntry { FullPath = @"C:\x\a.jpg", FileName = "a.jpg", Extension = "jpg", LastWriteTime = DateTime.Now },
            MatchedRule = new Rule { Actions = { new MoveAndRenameAction { Template = "{1}{ext}" } } },
            Status = PreviewStatus.TemplateError
        };
        var (result, _) = Organizer.Execute(new List<PreviewEntry> { p }, DateTime.Now);
        Assert.Single(result.Failed);
        Assert.Contains("模板", result.Failed[0].Reason);
    }
}