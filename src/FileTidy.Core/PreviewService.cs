using FileTidy.Core.Models;

namespace FileTidy.Core;

/// <summary>预览条目状态</summary>
public enum PreviewStatus
{
    /// <summary>将被移动</summary>
    Moved,
    /// <summary>无规则命中，留在原地</summary>
    NoMatch,
    /// <summary>目标已存在且未启用自动序号</summary>
    Conflict
}

/// <summary>一条文件的整理预览</summary>
public class PreviewEntry
{
    public required FileEntry File { get; init; }
    /// <summary>命中的规则；NoMatch 时为 null</summary>
    public Rule? MatchedRule { get; init; }
    /// <summary>目标路径（已唯一化）；NoMatch 时为 null</summary>
    public string? DestPath { get; init; }
    public PreviewStatus Status { get; init; }
}

/// <summary>预览计算：扫描已就绪，只做匹配与目标路径计算，不落地任何操作</summary>
public static class PreviewService
{
    /// <summary>为一批文件生成预览条目；每个文件至多被一条规则处理（先命中先得）</summary>
    public static List<PreviewEntry> Build(IReadOnlyList<Rule> rules, IEnumerable<FileEntry> files, DateTime now)
    {
        var previews = new List<PreviewEntry>();
        foreach (var file in files)
        {
            var rule = RuleEngine.FindFirstMatch(rules, file, now);
            if (rule is null)
            {
                previews.Add(new PreviewEntry { File = file, Status = PreviewStatus.NoMatch });
                continue;
            }

            var desired = Path.Combine(rule.TargetPath, file.FileName);
            var (dest, status) = ResolveDest(desired, rule);
            previews.Add(new PreviewEntry
            {
                File = file,
                MatchedRule = rule,
                DestPath = dest,
                Status = status
            });
        }
        return previews;
    }

    private static (string, PreviewStatus) ResolveDest(string desired, Rule rule)
    {
        if (File.Exists(desired) || Directory.Exists(desired))
        {
            return rule.AutoRenameOnConflict
                ? (PathHelper.GetUniquePath(desired), PreviewStatus.Moved)
                : (desired, PreviewStatus.Conflict);
        }
        return (desired, PreviewStatus.Moved);
    }
}