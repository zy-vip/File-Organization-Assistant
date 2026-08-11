namespace FileTidy.Core;

/// <summary>文件夹监听：文件新增/重命名/变更触发事件；缓冲溢出时自动重建监听并触发一次重扫</summary>
public sealed class FolderWatcher : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>有文件变更时触发（由调用方决定延迟与整理）</summary>
    public event Action? TidyTriggered;

    /// <summary>为单个文件夹创建监听并加入集合（Watch/Sync 共用）</summary>
    private void AddWatcher(string folder)
    {
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

    /// <summary>开始监听指定文件夹（已监听过的自动去重）</summary>
    public void Watch(IReadOnlyList<string> folders)
    {
        var targets = folders.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        lock (_lock)
        {
            if (_disposed) return;
            foreach (var folder in targets)
            {
                if (_watchers.Any(w => string.Equals(w.Path.TrimEnd('\\'), folder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
                    continue;
                AddWatcher(folder);
            }
        }
    }

    /// <summary>增量同步监听：新增目录建监听、已移除目录停监听，现存 watcher 不动（区别于 Replace 的全量重建）</summary>
    public void Sync(IReadOnlyList<string> folders)
    {
        var targets = folders.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        lock (_lock)
        {
            if (_disposed) return;
            foreach (var folder in targets)
            {
                if (_watchers.Any(w => string.Equals(w.Path.TrimEnd('\\'), folder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
                    continue;
                AddWatcher(folder);
            }
            foreach (var w in _watchers.ToList())
            {
                if (!targets.Any(t => string.Equals(t.TrimEnd('\\'), w.Path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
                {
                    w.Dispose();
                    _watchers.Remove(w);
                }
            }
        }
    }

    /// <summary>以新列表整体替换监听集合（先清空再按新列表 Watch），用于规则增删/源路径变更</summary>
    public void Replace(IReadOnlyList<string> folders)
    {
        lock (_lock)
        {
            foreach (var w in _watchers) w.Dispose();
            _watchers.Clear();
        }
        Watch(folders);
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
        => TidyTriggered?.Invoke();

    private void OnError(object sender, ErrorEventArgs e)
    {
        // 缓冲溢出或目录被删：对已不存在的目录释放 watcher（重建后由 Sync/Replace 重新监听），
        // 剩余 watcher 复位事件开关并触发一次重扫，避免溢出期间的文件变更永久丢失
        lock (_lock)
        {
            foreach (var w in _watchers.ToList())
            {
                if (Directory.Exists(w.Path)) continue;
                w.Dispose();
                _watchers.Remove(w);
            }
            foreach (var w in _watchers)
            {
                try { w.EnableRaisingEvents = false; w.EnableRaisingEvents = true; }
                catch { }
            }
        }
        if (_watchers.Count > 0) TidyTriggered?.Invoke();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var w in _watchers) w.Dispose();
            _watchers.Clear();
        }
    }
}