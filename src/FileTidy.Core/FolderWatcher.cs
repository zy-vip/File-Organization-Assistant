namespace FileTidy.Core;

/// <summary>文件夹监听：文件新增/重命名/变更触发事件；缓冲溢出时自动重建监听</summary>
public sealed class FolderWatcher : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = new();
    private bool _disposed;

    /// <summary>有文件变更时触发（由调用方决定延迟与整理）</summary>
    public event Action? TidyTriggered;

    /// <summary>开始监听指定文件夹（已监听过的自动去重）</summary>
    public void Watch(IReadOnlyList<string> folders)
    {
        foreach (var folder in folders.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (_watchers.Any(w => string.Equals(w.Path.TrimEnd('\\'), folder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
                continue;
            var watcher = new FileSystemWatcher(folder)
            {
                IncludeSubdirectories = true,
                InternalBufferSize = 64 * 1024
            };
            watcher.Created += OnChanged;
            watcher.Renamed += OnChanged;
            watcher.Changed += OnChanged;
            watcher.Error += OnError;
            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
        }
    }

    /// <summary>以新列表整体替换监听集合（先清空再按新列表 Watch），用于规则增删/源路径变更</summary>
    public void Replace(IReadOnlyList<string> folders)
    {
        foreach (var w in _watchers) w.Dispose();
        _watchers.Clear();
        Watch(folders);
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
        => TidyTriggered?.Invoke();

    private void OnError(object sender, ErrorEventArgs e)
    {
        // 缓冲溢出：重建监听（保留原路径），后续事件触发时由调用方做整目录重扫
        foreach (var w in _watchers.ToList())
        {
            try { w.EnableRaisingEvents = false; w.EnableRaisingEvents = true; }
            catch { }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var w in _watchers) w.Dispose();
        _watchers.Clear();
    }
}