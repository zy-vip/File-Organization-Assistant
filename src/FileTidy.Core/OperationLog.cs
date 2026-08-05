using System.Text.Json;

namespace FileTidy.Core;

/// <summary>一次文件移动</summary>
public class LogEntry
{
    public string Source { get; set; } = "";
    public string Dest { get; set; } = "";
}

/// <summary>一次整理操作记录（持久化于磁盘，供撤销）</summary>
public class OperationRecord
{
    public DateTime Timestamp { get; set; }
    public List<LogEntry> Entries { get; set; } = new();
}

/// <summary>操作日志：写盘、读取最近一份、作废、按保留份数清理</summary>
public class OperationLog
{
    private readonly string _dir;
    private readonly int _retention;

    public OperationLog(string logDirectory, int retention)
    {
        _dir = logDirectory;
        _retention = Math.Max(1, retention);
        Directory.CreateDirectory(_dir);
    }

    private IEnumerable<FileInfo> Files()
        => Directory.EnumerateFiles(_dir, "*.json")
                    .Select(p => new FileInfo(p))
                    .OrderBy(f => f.Name);

    /// <summary>保存一份记录（文件名含时间戳，天然有序）</summary>
    public void Save(OperationRecord record)
    {
        var path = Path.Combine(_dir, $"op-{record.Timestamp:yyyyMMddHHmmssfff}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(record));
        Trim();
    }

    /// <summary>最近一份记录；无则返回 null</summary>
    public OperationRecord? Latest()
    {
        var file = Files().LastOrDefault();
        if (file is null) return null;
        try
        {
            return JsonSerializer.Deserialize<OperationRecord>(File.ReadAllText(file.FullName));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>删除最近一份记录，并清理超出保留份数的旧记录</summary>
    public void DiscardLatest()
    {
        var file = Files().LastOrDefault();
        if (file is not null) file.Delete();
        Trim();
    }

    private void Trim()
    {
        var all = Files().ToList();
        foreach (var old in all.Take(all.Count - _retention)) old.Delete();
    }
}