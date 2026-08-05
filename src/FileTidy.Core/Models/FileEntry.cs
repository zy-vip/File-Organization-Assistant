namespace FileTidy.Core.Models;

/// <summary>扫描得到的候选文件</summary>
public class FileEntry
{
    /// <summary>文件完整路径</summary>
    public required string FullPath { get; init; }
    /// <summary>文件名（含扩展名）</summary>
    public required string FileName { get; init; }
    /// <summary>扩展名，不含点、小写；无扩展名为空串</summary>
    public required string Extension { get; init; }
    /// <summary>最后修改时间</summary>
    public DateTime LastWriteTime { get; init; }
}
