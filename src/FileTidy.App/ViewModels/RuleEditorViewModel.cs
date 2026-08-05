using System.IO;
using System.Runtime.CompilerServices;
using FileTidy.Core.Models;

namespace FileTidy.App.ViewModels;

/// <summary>规则编辑器 ViewModel（绑定中间层，ApplyToModel 落回 Rule；属性变更即触发即时校验）</summary>
public class RuleEditorViewModel : ObservableObject
{
    /// <summary>对应的领域模型（对象初始化器赋值，故 set 公开）</summary>
    public Rule Model { get; set; } = new();
    public string Name { get => _name; set => SetProperty(ref _name, value); }
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

    /// <summary>期限（天），0 表示不启用日期条件</summary>
    public int AgeDays { get => _age; set => SetProperty(ref _age, value); }
    private int _age = 0;

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
         + (AgeDays > 0 ? 1 : 0);

    /// <summary>将编辑器状态写回 Rule 模型</summary>
    public void ApplyToModel()
    {
        Model.Name = Name; Model.SourcePath = SourcePath; Model.TargetPath = TargetPath;
        Model.IncludeSubfolders = IncludeSubfolders; Model.ExcludeTargetTree = ExcludeTargetTree;
        Model.AutoRenameOnConflict = AutoRenameOnConflict;
        Model.Conditions.Clear();
        var exts = Extensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (exts.Length > 0) Model.Conditions.Add(new ExtensionCondition { Extensions = exts.ToList() });
        // 每个关键词独立成条件（任一命中即触发），避免逗号分隔输入被静默丢弃
        var kws = Keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var kw in kws)
            Model.Conditions.Add(new KeywordCondition { Keyword = kw });
        if (AgeDays > 0) Model.Conditions.Add(new AgeCondition { Days = AgeDays });
    }
}