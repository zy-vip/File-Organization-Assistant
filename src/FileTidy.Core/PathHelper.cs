namespace FileTidy.Core;

/// <summary>路径工具</summary>
public static class PathHelper
{
    /// <summary>目标路径已存在时追加 (1)、(2)… 返回可用路径</summary>
    public static string GetUniquePath(string desiredPath)
    {
        if (!File.Exists(desiredPath) && !Directory.Exists(desiredPath)) return desiredPath;
        var dir = Path.GetDirectoryName(desiredPath) ?? "";
        var name = Path.GetFileNameWithoutExtension(desiredPath);
        var ext = Path.GetExtension(desiredPath);
        for (var i = 1; ; i++)
        {
            var candidate = Path.Combine(dir, $"{name}({i}){ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
    }
}