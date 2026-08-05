namespace FileTidy.Core.Models;

/// <summary>整理规则：源文件夹 + 条件组 + 移动动作</summary>
public class Rule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string TargetPath { get; set; } = "";
    /// <summary>条件组：任一条件满足即触发</summary>
    public List<FileCondition> Conditions { get; set; } = new();
    /// <summary>是否递归扫描子文件夹</summary>
    public bool IncludeSubfolders { get; set; } = true;
    /// <summary>是否排除目标文件夹树（防循环）</summary>
    public bool ExcludeTargetTree { get; set; } = true;
    /// <summary>冲突时自动追加序号</summary>
    public bool AutoRenameOnConflict { get; set; } = true;
}
