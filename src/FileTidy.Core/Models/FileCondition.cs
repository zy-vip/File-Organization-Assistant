using System.Text.Json.Serialization;

namespace FileTidy.Core.Models;

/// <summary>规则条件基类（JSON 多态序列化）</summary>
[JsonDerivedType(typeof(ExtensionCondition), "extension")]
[JsonDerivedType(typeof(KeywordCondition), "keyword")]
[JsonDerivedType(typeof(AgeCondition), "age")]
[JsonDerivedType(typeof(RegexCondition), "regex")]
public abstract class FileCondition
{
    /// <summary>判断文件是否满足条件</summary>
    public abstract bool IsMatch(FileEntry file, DateTime now);

    /// <summary>所需 Pro 功能；null 表示免费条件</summary>
    public virtual ProFeature? RequiredFeature => null;
}

/// <summary>扩展名条件：扩展名 ∈ 列表（忽略大小写）</summary>
public sealed class ExtensionCondition : FileCondition
{
    public List<string> Extensions { get; set; } = new();

    public override bool IsMatch(FileEntry file, DateTime now)
        => Extensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase);
}

/// <summary>关键词条件：文件名包含关键词（忽略大小写）</summary>
public sealed class KeywordCondition : FileCondition
{
    public string Keyword { get; set; } = "";

    public override bool IsMatch(FileEntry file, DateTime now)
        => Keyword.Length > 0 && file.FileName.Contains(Keyword, StringComparison.OrdinalIgnoreCase);
}

/// <summary>日期条件：最后修改时间距今 ≥ N 天（N ≤ 0 视为未启用）</summary>
public sealed class AgeCondition : FileCondition
{
    public int Days { get; set; }

    public override bool IsMatch(FileEntry file, DateTime now)
        => Days > 0 && (now - file.LastWriteTime).TotalDays >= Days;
}
