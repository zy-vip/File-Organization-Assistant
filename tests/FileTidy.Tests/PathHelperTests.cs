// tests/FileTidy.Tests/PathHelperTests.cs
using System.IO;
using FileTidy.Core;

namespace FileTidy.Tests;

public class PathHelperTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("unique").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void GetUniquePath_AppendsNumberWhenExists()
    {
        var p1 = Path.Combine(_dir, "报告.pdf");
        File.WriteAllText(p1, "x");
        File.WriteAllText(Path.Combine(_dir, "报告(1).pdf"), "x");

        Assert.Equal(Path.Combine(_dir, "报告(2).pdf"), PathHelper.GetUniquePath(p1));
    }

    [Fact]
    public void GetUniquePath_ReturnsOriginalWhenFree()
    {
        var p = Path.Combine(_dir, "a.txt");
        Assert.Equal(p, PathHelper.GetUniquePath(p));
    }
}