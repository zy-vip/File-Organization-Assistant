// tests/FileTidy.Tests/RuleActionTests.cs
using System.IO;
using System.Text.Json;
using FileTidy.Core;
using FileTidy.Core.Models;

namespace FileTidy.Tests;

public class RuleActionTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("action").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void Serialize_ActionRoundTrips()
    {
        var path = Path.Combine(_dir, "c.json");
        var rule = new Rule
        {
            Name = "重命名",
            Actions = { new MoveAndRenameAction { Template = "{name}_{date:yyyyMMdd}{ext}" } }
        };
        File.WriteAllText(path, JsonSerializer.Serialize(rule));
        var loaded = JsonSerializer.Deserialize<Rule>(File.ReadAllText(path));

        Assert.NotNull(loaded);
        Assert.IsType<MoveAndRenameAction>(loaded!.Actions[0]);
        Assert.Equal("{name}_{date:yyyyMMdd}{ext}", ((MoveAndRenameAction)loaded.Actions[0]).Template);
    }

    [Fact]
    public void EffectiveAction_EmptyActionsReturnsMoveAction()
    {
        var rule = new Rule();
        Assert.IsType<MoveAction>(rule.EffectiveAction);
    }

    [Fact]
    public void EffectiveAction_UsesFirstAction()
    {
        var rename = new MoveAndRenameAction { Template = "x{ext}" };
        var rule = new Rule { Actions = { rename } };
        Assert.Same(rename, rule.EffectiveAction);
    }
}
