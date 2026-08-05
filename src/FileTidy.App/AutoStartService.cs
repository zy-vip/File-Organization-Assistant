using Microsoft.Win32;
using FileTidy.Core;

namespace FileTidy.App;

/// <summary>开机自启（注册表 Run 键）</summary>
public static class AutoStartService
{
    private const string ValueName = "FileTidy";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string v && v.Length > 0;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            // 带 --autostart 参数：启动时驻留托盘而不弹主窗口
            key?.SetValue(ValueName, $"\"{AppPaths.ExePath}\" --autostart");
        else
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}