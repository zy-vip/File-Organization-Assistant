// tests/FileTidy.Tests/FileScannerTests.cs
using System.IO;
using FileTidy.Core;

namespace FileTidy.Tests;

public class FileScannerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("scan").FullName;

    public void Dispose() => Directory.Delete(_root, true);

    private string Write(string relative)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        return path;
    }

    [Fact]
    public void Scan_RecursiveCollectsFiles()
    {
        Write("a.txt"); Write("sub/b.txt");
        var files = FileScanner.Scan(_root, includeSubfolders: true, Array.Empty<string>());
        Assert.Equal(2, files.Count);
        Assert.Contains(files, f => f.FileName == "b.txt");
    }

    [Fact]
    public void Scan_NonRecursiveOnlyTopLevel()
    {
        Write("a.txt"); Write("sub/b.txt");
        var files = FileScanner.Scan(_root, includeSubfolders: false, Array.Empty<string>());
        Assert.Single(files);
        Assert.Equal("a.txt", files[0].FileName);
    }

    [Fact]
    public void Scan_ExcludesTargetTree()
    {
        Write("a.txt");
        var outDir = Path.Combine(_root, "out");
        Write("out/keep.txt");
        var files = FileScanner.Scan(_root, true, new[] { outDir });
        Assert.Single(files);
        Assert.Equal("a.txt", files[0].FileName);
    }

    [Fact]
    public void Scan_MissingSource_ReturnsEmpty()
    {
        var files = FileScanner.Scan(Path.Combine(_root, "nope"), true, Array.Empty<string>());
        Assert.Empty(files);
    }
}