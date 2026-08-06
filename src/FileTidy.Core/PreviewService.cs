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
    Conflict,
    /// <summary>模板渲染失败，跳过</summary>
    TemplateError,
    /// <summary>规则需要 Pro 功能但未授权，跳过</summary>
    NeedsPro
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
    /// <summary>NeedsPro 时记录所需 Pro 功能中文名（/ 分隔）</summary>
    public string? BlockedFeature { get; init; }
}

/// <summary>预览计算：扫描已就绪，只做匹配与目标路径计算，不落地任何操作</summary>
public static class PreviewService
{
    /// <summary>为一批文件生成预览条目；每个文件至多被一条规则处理（先命中先得）。
    /// isAllowed 缺省 null=全部放行（保持既有调用兼容）。</summary>
    public static List<PreviewEntry> Build(IReadOnlyList<Rule> rules, IEnumerable<FileEntry> files, DateTime now, Func<ProFeature, bool>? isAllowed = null)
    {
        isAllowed ??= _ => true;
        var previews = new List<PreviewEntry>();
        var sequence = new Dictionary<Rule, int>();
        foreach (var file in files)
        {
            var rule = RuleEngine.FindFirstMatch(rules, file, now);
            if (rule is null)
            {
                previews.Add(new PreviewEntry { File = file, Status = PreviewStatus.NoMatch });
                continue;
            }
            if (!RuleAllowed(rule, isAllowed))
            {
                var blocked = rule.Conditions.Select(c => c.RequiredFeature)
                    .Concat(rule.Actions.Select(a => a.RequiredFeature))
                    .Where(f => f is not null && !isAllowed(f!.Value))
                    .Select(f => FeatureName(f!.Value));
                previews.Add(new PreviewEntry
                {
                    File = file, MatchedRule = rule, Status = PreviewStatus.NeedsPro,
                    BlockedFeature = string.Join(" / ", blocked)
                });
                continue;
            }

            var seq = NextSequence(sequence, rule);
            var (dest, status) = ResolveDest(rule, file, seq, now);
            previews.Add(new PreviewEntry { File = file, MatchedRule = rule, DestPath = dest, Status = status });
        }
        return previews;
    }

    private static bool RuleAllowed(Rule rule, Func<ProFeature, bool> isAllowed)
        => rule.Conditions.Select(c => c.RequiredFeature)
               .Concat(rule.Actions.Select(a => a.RequiredFeature))
               .Where(f => f is not null)
               .All(f => isAllowed(f!.Value));

    /// <summary>Pro 功能枚举 → 中文名（用于拦截原因与 UI 提示）</summary>
    private static string FeatureName(ProFeature feature) => feature switch
    {
        ProFeature.RegularExpression => "正则条件",
        ProFeature.RenameTemplate => "重命名模板",
        _ => feature.ToString()
    };

    private static int NextSequence(Dictionary<Rule, int> sequence, Rule rule)
    {
        sequence.TryGetValue(rule, out var n);
        sequence[rule] = n + 1;
        return n + 1;
    }

    private static (string?, PreviewStatus) ResolveDest(Rule rule, FileEntry file, int seq, DateTime now)
    {
        var desired = rule.EffectiveAction switch
        {
            MoveAndRenameAction rename => RenderDest(rule, file, rename.Template, seq, now),
            _ => Path.Combine(rule.TargetPath, file.FileName)
        };
        if (desired is null) return (null, PreviewStatus.TemplateError);

        if (File.Exists(desired) || Directory.Exists(desired))
            return rule.AutoRenameOnConflict
                ? (PathHelper.GetUniquePath(desired), PreviewStatus.Moved)
                : (desired, PreviewStatus.Conflict);
        return (desired, PreviewStatus.Moved);
    }

    /// <summary>渲染重命名模板：捕获组取自规则第一个 RegexCondition；失败返回 null</summary>
    private static string? RenderDest(Rule rule, FileEntry file, string template, int seq, DateTime now)
    {
        var match = rule.Conditions.OfType<RegexCondition>().FirstOrDefault()?.Match(file);
        var result = TemplateRenderer.Render(template, file, match, seq, now);
        if (!result.Success) return null;
        return Path.Combine(rule.TargetPath, result.FileName!);
    }
}