using FileTidy.Core.Models;

namespace FileTidy.Core;

/// <summary>规则匹配引擎</summary>
public static class RuleEngine
{
    /// <summary>判断文件是否满足规则的任一条件；无条件时视为不匹配</summary>
    public static bool IsMatch(Rule rule, FileEntry file, DateTime now)
        => rule.Conditions.Count > 0 && rule.Conditions.Any(c => c.IsMatch(file, now));

    /// <summary>按规则列表顺序返回第一个命中的规则；无命中返回 null</summary>
    public static Rule? FindFirstMatch(IReadOnlyList<Rule> rules, FileEntry file, DateTime now)
        => rules.FirstOrDefault(r => IsMatch(r, file, now));
}