using System.IO;
using System.Runtime.CompilerServices;
using FileTidy.Core;
using FileTidy.Core.Models;

namespace FileTidy.App.ViewModels;

/// <summary>规则编辑器 ViewModel（绑定中间层，ApplyToModel 落回 Rule；属性变更即触发即时校验）</summary>
public class RuleEditorViewModel : ObservableObject
{
    /// <summary>对应的领域模型（对象初始化器赋值，故 set 公开）</summary>
    public Rule Model { get; set; } = new();
    public string Name { get => _name; set { if (SetProperty(ref _name, value)) OnPropertyChanged(nameof(DisplayName)); } }

    /// <summary>左侧列表显示用：未命名规则显示占位符，避免空行</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "（新规则）" : Name.Trim();
    public string SourcePath { get => _source; set => SetProperty(ref _source, value); }
    public string TargetPath { get => _target; set => SetProperty(ref _target, value); }
    public bool IncludeSubfolders { get => _subs; set => SetProperty(ref _subs, value); }
    public bool ExcludeTargetTree { get => _exclude; set => SetProperty(ref _exclude, value); }
    public bool AutoRenameOnConflict { get => _rename; set => SetProperty(ref _rename, value); }
    private string _name = ""; private string _source = ""; private string _target = "";
    private bool _subs = true; private bool _exclude = true; private bool _rename = true;

    /// <summary>扩展名列表字符串（逗号分隔）</summary>
    public string Extensions { get => _exts; set => SetProperty(ref _exts, value); }
    private string _exts = "";

    /// <summary>关键词列表字符串（逗号分隔）</summary>
    public string Keywords { get => _keywords; set => SetProperty(ref _keywords, value); }
    private string _keywords = "";

    /// <summary>期限（天）文本，空/非法按 0 处理；0 表示不启用日期条件</summary>
    public string AgeDays { get => _age; set => SetProperty(ref _age, value); }
    private string _age = "0";

    /// <summary>正则条件文本（Pro）；非空即启用正则条件</summary>
    public string RegexPattern { get => _regex; set => SetProperty(ref _regex, value); }
    private string _regex = "";

    /// <summary>正则是否区分大小写（默认忽略大小写）</summary>
    public bool RegexCaseSensitive { get => _case; set => SetProperty(ref _case, value); }
    private bool _case = false;

    /// <summary>动作类型：move（仅移动） / moveRename（移动并重命名，Pro）</summary>
    public string ActionType { get => _action; set => SetProperty(ref _action, value); }
    private string _action = "move";

    /// <summary>重命名模板（Pro，选中 moveRename 时生效）</summary>
    public string RenameTemplate { get => _template; set => SetProperty(ref _template, value); }
    private string _template = "";

    /// <summary>解析后的有效期天数，非法或非正数返回 0（禁用）</summary>
    private int AgeDaysParsed => int.TryParse(AgeDays, out var days) && days > 0 ? days : 0;

    private List<string>? _errors;

    /// <summary>校验错误摘要（只读计算属性），界面即时显示；无错误为 null</summary>
    public string? ErrorSummary => _errors is { Count: > 0 } ? string.Join("；", _errors) : null;

    /// <summary>任意属性变更后自动重新校验（即时校验提示）</summary>
    protected override bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        var changed = base.SetProperty(ref field, value, name);
        if (changed) RefreshErrors();
        return changed;
    }

    public void AddExtension(string ext) => Extensions = Extensions.Length == 0 ? ext : $"{Extensions}, {ext}";

    /// <summary>校验，返回错误信息列表；空列表即通过。结果同步到 ErrorSummary。</summary>
    public List<string> Validate()
    {
        _errors = ComputeErrors();
        OnPropertyChanged(nameof(ErrorSummary));
        return _errors;
    }

    private void RefreshErrors()
    {
        _errors = ComputeErrors();
        OnPropertyChanged(nameof(ErrorSummary));
    }

    private List<string> ComputeErrors()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Name)) errors.Add("规则名称不能为空");
        if (string.IsNullOrWhiteSpace(SourcePath) || !Directory.Exists(SourcePath)) errors.Add("源文件夹不存在");
        if (string.IsNullOrWhiteSpace(TargetPath)) errors.Add("目标文件夹不能为空");
        if (ConditionsCount() == 0) errors.Add("至少需要一个条件");
        if (!string.IsNullOrWhiteSpace(RegexPattern) && !RegexCondition.IsValidPattern(RegexPattern))
            errors.Add("正则表达式不合法");
        if (ActionType == "moveRename")
        {
            var tErrors = TemplateRenderer.Validate(RenameTemplate);
            errors.AddRange(tErrors.Select(e => "模板：" + e));
        }
        CheckTreeRelation(errors);
        return errors;
    }

    /// <summary>检查源/目标树包含关系：目标在源内部会导致循环整理，源在目标内部会被排除目标树逻辑滤空</summary>
    private void CheckTreeRelation(List<string> errors)
    {
        if (TargetPath.Length == 0 || SourcePath.Length == 0) return;
        try
        {
            var src = Path.GetFullPath(SourcePath).TrimEnd('\\') + '\\';
            var tgt = Path.GetFullPath(TargetPath).TrimEnd('\\');
            if (tgt.StartsWith(src, StringComparison.OrdinalIgnoreCase))
                errors.Add("目标文件夹位于源文件夹内部，可能导致循环整理");
            else if (src.StartsWith(tgt + '\\', StringComparison.OrdinalIgnoreCase))
                errors.Add("源文件夹位于目标文件夹内部，源文件会被排除导致无文件可整理（循环风险）");
        }
        catch (ArgumentException)
        {
            errors.Add("路径包含非法字符");
        }
        catch (PathTooLongException)
        {
            errors.Add("路径过长");
        }
        catch (NotSupportedException)
        {
            errors.Add("路径格式不受支持");
        }
    }

    private int ConditionsCount()
        => (Extensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length > 0 ? 1 : 0)
         + (Keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length > 0 ? 1 : 0)
         + (AgeDaysParsed > 0 ? 1 : 0)
         + (RegexPattern.Length > 0 ? 1 : 0);

    /// <summary>将编辑器状态写回 Rule 模型</summary>
    public void ApplyToModel()
    {
        Model.Name = Name; Model.SourcePath = SourcePath; Model.TargetPath = TargetPath;
        Model.IncludeSubfolders = IncludeSubfolders; Model.ExcludeTargetTree = ExcludeTargetTree;
        Model.AutoRenameOnConflict = AutoRenameOnConflict;
        Model.Conditions.Clear();
        if (RegexPattern.Length > 0)
            Model.Conditions.Add(new RegexCondition { Pattern = RegexPattern, IgnoreCase = !RegexCaseSensitive });
        var exts = Extensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (exts.Length > 0) Model.Conditions.Add(new ExtensionCondition { Extensions = exts.ToList() });
        // 每个关键词独立成条件（任一命中即触发），避免逗号分隔输入被静默丢弃
        var kws = Keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var kw in kws)
            Model.Conditions.Add(new KeywordCondition { Keyword = kw });
        if (AgeDaysParsed > 0) Model.Conditions.Add(new AgeCondition { Days = AgeDaysParsed });
        Model.Actions.Clear();
        Model.Actions.Add(ActionType == "moveRename" && TemplateRenderer.Validate(RenameTemplate).Count == 0
            ? new MoveAndRenameAction { Template = RenameTemplate }
            : new MoveAction());
    }
}