// tests/FileTidy.Tests/ConfigSerializerTests.cs
using System.IO;
using FileTidy.Core;
using FileTidy.Core.Models;

namespace FileTidy.Tests;

public class ConfigSerializerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("filedity").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void SaveThenLoad_ReturnsSameConfig()
    {
        var path = Path.Combine(_dir, "config.json");
        var config = new FileTidyConfig
        {
            AutoTidyEnabled = true,
            AutoRenameOnConflict = false,
            OperationLogRetention = 5,
            Rules =
            {
                new Rule
                {
                    Name = "图片",
                    SourcePath = @"C:\Downloads",
                    TargetPath = @"D:\Pictures",
                    Conditions =
                    {
                        new ExtensionCondition { Extensions = { "jpg", "png" } },
                        new AgeCondition { Days = 7 }
                    }
                }
            }
        };

        ConfigSerializer.Save(config, path);
        var loaded = ConfigSerializer.Load(path);

        Assert.Equal(config.AutoTidyEnabled, loaded.AutoTidyEnabled);
        Assert.Equal(config.AutoRenameOnConflict, loaded.AutoRenameOnConflict);
        Assert.Equal(config.OperationLogRetention, loaded.OperationLogRetention);
        Assert.Single(loaded.Rules);
        Assert.Equal("图片", loaded.Rules[0].Name);
        Assert.Equal(2, loaded.Rules[0].Conditions.Count);
        Assert.IsType<ExtensionCondition>(loaded.Rules[0].Conditions[0]);
        Assert.Equal(new List<string> { "jpg", "png" }, ((ExtensionCondition)loaded.Rules[0].Conditions[0]).Extensions);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaultConfig()
    {
        var path = Path.Combine(_dir, "none.json");
        var loaded = ConfigSerializer.Load(path);
        Assert.Empty(loaded.Rules);
        Assert.False(loaded.AutoTidyEnabled);
    }

    [Fact]
    public void Load_CorruptedJson_ReturnsDefaultConfig()
    {
        var path = Path.Combine(_dir, "broken.json");
        File.WriteAllText(path, "{ 这不是合法 JSON !!!");
        var loaded = ConfigSerializer.Load(path);
        Assert.Empty(loaded.Rules);
    }
}
