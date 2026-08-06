using System.Text.RegularExpressions;
using FileTidy.Core.Models;

namespace FileTidy.Core;

/// <summary>模板渲染结果</summary>
public class TemplateRenderResult
{
    public bool Success { get; init; }
    public string? FileName { get; init; }
    public string? Error { get; init; }
    public static TemplateRenderResult Ok(string fileName) => new() { Success = true, FileName = fileName };
    public static TemplateRenderResult Fail(string error) => new() { Success = false, Error = error };
}

/// <summary>重命名模板引擎。变量：{original} {name} {ext} {date:格式} {n} {1} {2}…</summary>
public static class TemplateRenderer
{
    private const string IllegalChars = "<>:\"/\\|?*";
    private static readonly Regex VariableRe = new(@"\{([^{}]*)\}", RegexOptions.Compiled);

    /// <summary>结构校验（编辑时用）：返回错误列表，空即通过。运行时错误（空结果、捕获组缺失）由 Render 返回。</summary>
    public static List<string> Validate(string template)
    {
        var errors = new List<string>();
        if (string.IsNullOrEmpty(template)) { errors.Add("模板不能为空"); return errors; }
        if (template.Count(c => c == '{') != template.Count(c => c == '}'))
            errors.Add("花括号未闭合");
        // 非法字符检查需排除 {date:...} 内的冒号：日期格式自身的合法语法
        var sansDateFormat = VariableRe.Replace(template, m =>
            m.Groups[1].Value.StartsWith("date:", StringComparison.Ordinal) ? "d" : m.Value);
        if (sansDateFormat.IndexOfAny(IllegalChars.ToCharArray()) >= 0)
            errors.Add("模板包含非法字符（< > : \" / \\ | ? *）");
        foreach (Match m in VariableRe.Matches(template))
        {
            var name = m.Groups[1].Value;
            if (name.Length == 0) { errors.Add("存在空的 { } 变量"); continue; }
            if (name.StartsWith("date:", StringComparison.Ordinal))
            {
                var fmt = name["date:".Length..];
                if (fmt.Length == 0) { errors.Add("{date:} 缺少日期格式"); continue; }
                try { _ = DateTime.Now.ToString(fmt); }
                catch (FormatException) { errors.Add($"日期格式 {fmt} 无效"); }
                continue;
            }
            if (name is "original" or "name" or "ext" or "n") continue;
            if (int.TryParse(name, out var idx))
            {
                // 设计只定义 {1} 起的捕获组：{0}（等价完整匹配）与负数均拒绝
                if (idx < 1) errors.Add($"捕获组索引 {idx} 无效（从 1 开始）");
                continue;
            }
            errors.Add($"未知变量 {{{name}}}");
        }
        return errors;
    }

    /// <summary>渲染模板得到目标文件名。sequence 为该规则内批序号（1 基）。</summary>
    public static TemplateRenderResult Render(string template, FileEntry file, RegexMatchResult? match, int sequence, DateTime now)
    {
        var structural = Validate(template);
        if (structural.Count > 0) return TemplateRenderResult.Fail(string.Join("；", structural));

        var name = Path.GetFileNameWithoutExtension(file.FileName);
        var ext = file.Extension.Length > 0 ? "." + file.Extension : "";
        string result;
        try
        {
            result = VariableRe.Replace(template, m =>
            {
                var v = m.Groups[1].Value;
                return v switch
                {
                    "original" => file.FileName,
                    "name" => name,
                    "ext" => ext,
                    "n" => sequence.ToString(),
                    _ when v.StartsWith("date:", StringComparison.Ordinal) => now.ToString(v["date:".Length..]),
                    _ when int.TryParse(v, out var idx) => CaptureOrEmpty(match, idx),
                    _ => m.Value // Validate 已保证不会走到未知变量
                };
            });
        }
        catch (InvalidOperationException ex)
        {
            // 捕获组缺失或引用无正则匹配：按渲染失败返回（用户约定的修正 A）
            return TemplateRenderResult.Fail(ex.Message);
        }

        if (result.Length == 0) return TemplateRenderResult.Fail("渲染结果为空");
        if (result.IndexOfAny(IllegalChars.ToCharArray()) >= 0)
            return TemplateRenderResult.Fail("渲染结果包含非法字符");
        return TemplateRenderResult.Ok(result);
    }

    private static string CaptureOrEmpty(RegexMatchResult? match, int index)
    {
        if (match is null) throw new InvalidOperationException("捕获组引用但规则无正则匹配结果");
        // 索引从 1 起（设计语义）；可选捕获组未参与匹配时值为空串——属于该组合法匹配结果，
        // 直接使用不判错误（误判会破坏「可选命名」合法模板），空结果由「渲染结果为空」兜底
        if (index < 1 || index >= match.Groups.Count) throw new InvalidOperationException($"捕获组 {index} 未匹配");
        return match.Groups[index];
    }
}