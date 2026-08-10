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
    /// <summary>单次匹配超时（毫秒）：防御灾难性回溯（ReDoS）卡死调用线程（预览在 UI 线程执行）</summary>
    private const int MatchTimeoutMs = 500;

    /// <summary>正则表达式</summary>
    public string Pattern { get; set; } = "";
    /// <summary>忽略大小写，默认开启</summary>
    public bool IgnoreCase { get; set; } = true;

    private RegexOptions Options => IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;

    // 编译结果缓存：Pattern/IgnoreCase 变化时失效，避免每次匹配重复编译
    private Regex? _compiled;
    private string? _compiledPattern;
    private bool _compiledIgnoreCase;

    /// <summary>正则是否合法（编译不抛异常）</summary>
    public static bool IsValidPattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return false;
        try { _ = new Regex(pattern); return true; }
        catch (ArgumentException) { return false; }
    }

    public override bool IsMatch(FileEntry file, DateTime now)
    {
        var re = TryGetRegex();
        if (re is null) return false;
        try { return re.IsMatch(file.FileName); }
        catch (RegexMatchTimeoutException) { return false; }
    }

    /// <summary>返回完整匹配与捕获组；未命中或匹配超时返回 null</summary>
    public RegexMatchResult? Match(FileEntry file)
    {
        var re = TryGetRegex();
        if (re is null) return null;
        Match m;
        try { m = re.Match(file.FileName); }
        catch (RegexMatchTimeoutException) { return null; }
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
        if (_compiled is not null && _compiledPattern == Pattern && _compiledIgnoreCase == IgnoreCase)
            return _compiled;
        try
        {
            _compiled = new Regex(Pattern, Options, TimeSpan.FromMilliseconds(MatchTimeoutMs));
            _compiledPattern = Pattern;
            _compiledIgnoreCase = IgnoreCase;
            return _compiled;
        }
        catch (ArgumentException)
        {
            _compiled = null;
            return null;
        }
    }
}
