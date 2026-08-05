using System.Text.Json;
using FileTidy.Core.Models;

namespace FileTidy.Core;

/// <summary>配置文件的读写</summary>
public static class ConfigSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>保存配置到指定路径（目录自动创建）</summary>
    public static void Save(FileTidyConfig config, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(config, Options));
    }

    /// <summary>从指定路径加载配置；文件不存在或损坏时返回默认配置</summary>
    public static FileTidyConfig Load(string path)
    {
        if (!File.Exists(path)) return new FileTidyConfig();
        try
        {
            return JsonSerializer.Deserialize<FileTidyConfig>(File.ReadAllText(path), Options)
                   ?? new FileTidyConfig();
        }
        catch (JsonException)
        {
            return new FileTidyConfig();
        }
    }
}
