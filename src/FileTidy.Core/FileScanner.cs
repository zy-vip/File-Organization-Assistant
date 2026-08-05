using FileTidy.Core.Models;

namespace FileTidy.Core;

/// <summary>扫描源文件夹，收集候选文件</summary>
public static class FileScanner
{
    /// <summary>扫描 sourcePath 下所有文件。
    /// includeSubfolders 为 false 时仅扫描第一层；
    /// excludeRoots 中的目录（含子路径）被跳过，用于排除目标文件夹防循环。</summary>
    public static List<FileEntry> Scan(string sourcePath, bool includeSubfolders, IReadOnlyCollection<string> excludeRoots)
    {
        var results = new List<FileEntry>();
        if (!Directory.Exists(sourcePath)) return results;

        var excludeSet = new HashSet<string>(excludeRoots.Select(Full), StringComparer.OrdinalIgnoreCase);
        ScanDir(sourcePath, includeSubfolders, excludeSet, results);
        return results;
    }

    private static void ScanDir(string dir, bool recursive, HashSet<string> excludeSet, List<FileEntry> results)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                if (excludeSet.Contains(Full(file))) continue;
                var fi = new FileInfo(file);
                results.Add(new FileEntry
                {
                    FullPath = file,
                    FileName = fi.Name,
                    Extension = fi.Extension.TrimStart('.').ToLowerInvariant(),
                    LastWriteTime = fi.LastWriteTime
                });
            }
        }
        catch (UnauthorizedAccessException) { /* 无权限子目录：跳过，不中断整个扫描 */ }
        catch (IOException) { /* 目录被并发删除/占用：跳过 */ }
        if (!recursive) return;
        try
        {
            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                if (excludeSet.Contains(Full(sub))) continue;
                ScanDir(sub, true, excludeSet, results);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }

    private static string Full(string path) => Path.GetFullPath(path).TrimEnd('\\', '/') + '\\';
}