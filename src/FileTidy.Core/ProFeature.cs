namespace FileTidy.Core;

/// <summary>Pro 功能标记：用于许可证门控</summary>
public enum ProFeature
{
    /// <summary>正则表达式条件</summary>
    RegularExpression,
    /// <summary>重命名模板动作</summary>
    RenameTemplate,
    /// <summary>重复文件检测（预留，本期不实现）</summary>
    DuplicateDetection
}
