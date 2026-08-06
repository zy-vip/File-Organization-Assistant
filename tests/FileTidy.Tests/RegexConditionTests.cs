// tests/FileTidy.Tests/RegexConditionTests.cs
using System.IO;
using System.Text.Json;
using FileTidy.Core;
using FileTidy.Core.Models;

namespace FileTidy.Tests;

public class RegexConditionTests
{
    private static FileEntry MakeFile(string name) => new()
    {
        FullPath = Path.Combine(@"C:\tmp", name),
        FileName = name,
        Extension = Path.GetExtension(name).TrimStart('.').ToLowerInvariant(),
        LastWriteTime = DateTime.Now
    };

    [Fact]
    public void IsMatch_MatchesFullFileName()
    {
        var c = new RegexCondition { Pattern = @"^IMG_\d{8}\.jpg$" };
        Assert.True(c.IsMatch(MakeFile("IMG_20260101.jpg"), DateTime.Now));
        Assert.False(c.IsMatch(MakeFile("IMG_20260101.png"), DateTime.Now));
        Assert.False(c.IsMatch(MakeFile("xIMG_20260101.jpg"), DateTime.Now));
    }

    [Fact]
    public void IsMatch_IgnoreCaseDefaultTrue()
    {
        var c = new RegexCondition { Pattern = @"^report-final\.pdf$" };
        Assert.True(c.IsMatch(MakeFile("REPORT-FINAL.PDF"), DateTime.Now));
        c.IgnoreCase = false;
        Assert.False(c.IsMatch(MakeFile("REPORT-FINAL.PDF"), DateTime.Now));
    }

    [Fact]
    public void Match_ReturnsCaptureGroups()
    {
        var c = new RegexCondition { Pattern = @"(\d{4})-(\d{2})" };
        var m = c.Match(MakeFile("2026-08-report.pdf"));
        Assert.NotNull(m);
        Assert.Equal("2026-08", m!.Groups[0]);
        Assert.Equal("2026", m.Groups[1]);
        Assert.Equal("08", m.Groups[2]);
        Assert.Null(c.Match(MakeFile("no-number.pdf")));
    }

    [Fact]
    public void IsValidPattern_RejectsInvalidRegex()
    {
        Assert.True(RegexCondition.IsValidPattern(@"^\d+$"));
        Assert.False(RegexCondition.IsValidPattern("("));
    }

    [Fact]
    public void RequiredFeature_IsRegularExpression()
    {
        Assert.Equal(ProFeature.RegularExpression, new RegexCondition().RequiredFeature);
    }

    [Fact]
    public void Serialize_RegexConditionRoundTrips()
    {
        var dir = Directory.CreateTempSubdirectory("regex").FullName;
        var path = Path.Combine(dir, "c.json");
        var rule = new Rule { Conditions = { new RegexCondition { Pattern = @"^IMG", IgnoreCase = false } } };
        File.WriteAllText(path, JsonSerializer.Serialize(rule));
        var loaded = JsonSerializer.Deserialize<Rule>(File.ReadAllText(path));
        var c = Assert.IsType<RegexCondition>(loaded!.Conditions[0]);
        Assert.Equal(@"^IMG", c.Pattern);
        Assert.False(c.IgnoreCase);
    }
}
