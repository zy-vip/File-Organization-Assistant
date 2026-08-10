using System.Text.Json.Serialization;

namespace FileTidy.Core.Models;

/// <summary>规则动作基类（JSON 多态序列化）。动作决定「做什么」，目标文件夹仍在 Rule.TargetPath 上。</summary>
[JsonDerivedType(typeof(MoveAction), "move")]
[JsonDerivedType(typeof(MoveAndRenameAction), "moveRename")]
public abstract class RuleAction
{
}

/// <summary>纯移动动作（免费）</summary>
public sealed class MoveAction : RuleAction
{
}

/// <summary>移动 + 重命名模板动作（Pro）</summary>
public sealed class MoveAndRenameAction : RuleAction
{
    /// <summary>重命名模板</summary>
    public string Template { get; set; } = "";
}
