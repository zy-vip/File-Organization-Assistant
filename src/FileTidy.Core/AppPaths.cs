namespace FileTidy.Core;

/// <summary>应用数据目录</summary>
public static class AppPaths
{
    /// <summary>根目录：%AppData%\FileTidy</summary>
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileTidy");

    /// <summary>配置文件路径</summary>
    public static string ConfigFile => Path.Combine(Root, "config.json");

    /// <summary>操作日志目录</summary>
    public static string OperationsDir => Path.Combine(Root, "operations");

    /// <summary>当前可执行文件路径</summary>
    public static string ExePath => Environment.ProcessPath ?? "";

    /// <summary>确保根目录与日志目录存在，返回 Root</summary>
    public static string EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(OperationsDir);
        return Root;
    }
}
