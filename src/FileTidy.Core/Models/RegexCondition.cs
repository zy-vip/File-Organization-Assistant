using System.Text.RegularExpressions;

namespace FileTidy.Core.Models;

/// <summary>正则匹配结果：Groups[0] 为完整匹配，Groups[1..] 为捕获组</summary>
public class RegexMatchResult
{
    public required IReadOnlyList<string> Groups { get; init; }
}

/// <summary>正则条件：以正则匹配完整文件名（含扩展名）（Pro）</summary>
public sealed class RegexCondition : FileCondition
{
    /// <summary>正则表达式</summary>
    public string Pattern { get; set; } = "";
    /// <summary>忽略大小写，默认开启</summary>
    public bool IgnoreCase { get; set; } = true;

    public override ProFeature? RequiredFeature => ProFeature.RegularExpression;

    private RegexOptions Options => IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;

    /// <summary>正则是否合法（编译不抛异常）</summary>
    public static bool IsValidPattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return false;
        try { _ = new Regex(pattern); return true; }
        catch (ArgumentException) { return false; }
    }

    public override bool IsMatch(FileEntry file, DateTime now)
        => TryGetRegex()?.IsMatch(file.FileName) == true;

    /// <summary>返回完整匹配与捕获组；未命中返回 null</summary>
    public RegexMatchResult? Match(FileEntry file)
    {
        var re = TryGetRegex();
        if (re is null) return null;
        var m = re.Match(file.FileName);
        if (!m.Success) return null;
        return new RegexMatchResult
        {
            Groups = m.Groups.Cast<Group>()
                              .Select(g => g.Value)
                              .ToArray()
        };
    }

    private Regex? TryGetRegex()
    {
        try { return new Regex(Pattern, Options); }
        catch (ArgumentException) { return null; }
    }
}
