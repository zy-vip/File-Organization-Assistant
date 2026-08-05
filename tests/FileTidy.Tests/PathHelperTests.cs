// tests/FileTidy.Tests/PathHelperTests.cs
using System.IO;
using FileTidy.Core;

namespace FileTidy.Tests;

public class PathHelperTests
{
    [Fact]
    public void GetUniquePath_AppendsNumberWhenExists()
    {
        var dir = Directory.CreateTempSubdirectory("unique").FullName;
        var p1 = Path.Combine(dir, "报告.pdf");
        File.WriteAllText(p1, "x");
        File.WriteAllText(Path.Combine(dir, "报告(1).pdf"), "x");

        Assert.Equal(Path.Combine(dir, "报告(2).pdf"), PathHelper.GetUniquePath(p1));
    }

    [Fact]
    public void GetUniquePath_ReturnsOriginalWhenFree()
    {
        var dir = Directory.CreateTempSubdirectory("unique").FullName;
        var p = Path.Combine(dir, "a.txt");
        Assert.Equal(p, PathHelper.GetUniquePath(p));
    }
}