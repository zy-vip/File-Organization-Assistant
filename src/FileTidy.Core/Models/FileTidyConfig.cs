namespace FileTidy.Core.Models;

/// <summary>全局配置</summary>
public class FileTidyConfig
{
    public List<Rule> Rules { get; set; } = new();
    public bool AutoTidyEnabled { get; set; } = false;
    public bool AutoRenameOnConflict { get; set; } = true;
    public int OperationLogRetention { get; set; } = 10;
    public bool StartWithWindows { get; set; } = false;
}
