using FileTidy.Core;
using FileTidy.Core.Models;

namespace FileTidy.App;

/// <summary>配置加载与保存（路径由构造参数注入，便于测试）</summary>
public class SettingsService
{
    private readonly string _configPath;
    public SettingsService(string configPath) => _configPath = configPath;
    public FileTidyConfig Load() => ConfigSerializer.Load(_configPath);
    public void Save(FileTidyConfig config) => ConfigSerializer.Save(config, _configPath);
}