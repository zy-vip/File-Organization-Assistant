// tests/FileTidy.Tests/RuleEngineTests.cs
using System.IO;
using FileTidy.Core;
using FileTidy.Core.Models;

namespace FileTidy.Tests;

public class RuleEngineTests
{
    private static FileEntry File(string name, DateTime? lastWrite = null)
        => new()
        {
            FullPath = Path.Combine(@"C:\tmp", name),
            FileName = name,
            Extension = Path.GetExtension(name).TrimStart('.').ToLowerInvariant(),
            LastWriteTime = lastWrite ?? DateTime.Now
        };

    [Fact]
    public void Extension_MatchesCaseInsensitive()
    {
        var rule = new Rule { Conditions = { new ExtensionCondition { Extensions = { "JPG", "Png" } } } };
        Assert.True(RuleEngine.IsMatch(rule, File("a.PNG"), DateTime.Now));
        Assert.False(RuleEngine.IsMatch(rule, File("a.gif"), DateTime.Now));
    }

    [Fact]
    public void Keyword_MatchesContainIgnoreCase()
    {
        var rule = new Rule { Conditions = { new KeywordCondition { Keyword = "report" } } };
        Assert.True(RuleEngine.IsMatch(rule, File("2026-REPORT-final.pdf"), DateTime.Now));
        Assert.False(RuleEngine.IsMatch(rule, File("photo.jpg"), DateTime.Now));
    }

    [Fact]
    public void Age_BoundaryIsInclusive()
    {
        var now = new DateTime(2026, 8, 5, 12, 0, 0);
        var rule = new Rule { Conditions = { new AgeCondition { Days = 3 } } };
        Assert.True(RuleEngine.IsMatch(rule, File("old.txt", now.AddDays(-3)), now));
        Assert.False(RuleEngine.IsMatch(rule, File("new.txt", now.AddDays(-2)), now));
    }

    [Fact]
    public void Age_NegativeDaysNeverMatches()
    {
        // 配置被手工改成负值时不应让所有文件永久命中
        var rule = new Rule { Conditions = { new AgeCondition { Days = -1 } } };
        Assert.False(RuleEngine.IsMatch(rule, File("old.txt", DateTime.Now.AddYears(-1)), DateTime.Now));
    }

    [Fact]
    public void Conditions_OrSemantics()
    {
        var rule = new Rule
        {
            Conditions =
            {
                new ExtensionCondition { Extensions = { "jpg" } },
                new KeywordCondition { Keyword = "urgent" }
            }
        };
        Assert.True(RuleEngine.IsMatch(rule, File("x.jpg"), DateTime.Now));
        Assert.True(RuleEngine.IsMatch(rule, File("URGENT.txt"), DateTime.Now));
        Assert.False(RuleEngine.IsMatch(rule, File("normal.txt"), DateTime.Now));
    }

    [Fact]
    public void FindFirstMatch_ReturnsFirstRuleInOrder()
    {
        var r1 = new Rule { Name = "第一", Conditions = { new KeywordCondition { Keyword = "install" } } };
        var r2 = new Rule { Name = "第二", Conditions = { new ExtensionCondition { Extensions = { "exe" } } } };
        var match = RuleEngine.FindFirstMatch(new List<Rule> { r1, r2 }, File("installer.exe"), DateTime.Now);
        Assert.Same(r1, match);
    }

    [Fact]
    public void FindFirstMatch_ReturnsNullWhenNoRuleMatches()
    {
        var r1 = new Rule { Conditions = { new KeywordCondition { Keyword = "zzz" } } };
        Assert.Null(RuleEngine.FindFirstMatch(new List<Rule> { r1 }, File("a.txt"), DateTime.Now));
    }
}