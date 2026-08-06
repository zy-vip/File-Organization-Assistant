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

    [Fact]
    public void GetUniquePath_NoExtension_AppendsNumber()
    {
        // 无扩展名文件：扩展名为空时序号拼接不得出错
        var p = Path.Combine(_dir, "报告");
        File.WriteAllText(p, "x");

        Assert.Equal(Path.Combine(_dir, "报告(1)"), PathHelper.GetUniquePath(p));
    }

    [Fact]
    public void GetUniquePath_DirectoryOccupied_AppendsNumber()
    {
        // 目标路径被目录占用时同样追加序号（Directory.Exists 分支）
        var p = Path.Combine(_dir, "目录");
        Directory.CreateDirectory(p);

        Assert.Equal(Path.Combine(_dir, "目录(1)"), PathHelper.GetUniquePath(p));
    }
}