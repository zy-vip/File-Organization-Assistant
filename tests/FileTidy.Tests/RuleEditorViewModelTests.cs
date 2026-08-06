// tests/FileTidy.Tests/RuleEditorViewModelTests.cs
using System.IO;
using FileTidy.App.ViewModels;
using FileTidy.Core.Models;

namespace FileTidy.Tests;

public class RuleEditorViewModelTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ed").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void Validation_InvalidRegexReportsError()
    {
        var vm = new RuleEditorViewModel();
        vm.Name = "r"; vm.SourcePath = _dir;
        vm.TargetPath = Path.Combine(vm.SourcePath, "out"); vm.Extensions = "jpg";
        vm.RegexPattern = "(";
        var errors = vm.Validate();
        Assert.Contains(errors, e => e.Contains("正则"));
    }

    [Fact]
    public void Validation_InvalidTemplateReportsError()
    {
        var vm = new RuleEditorViewModel();
        vm.Name = "r"; vm.SourcePath = _dir;
        vm.TargetPath = Path.Combine(vm.SourcePath, "out"); vm.Extensions = "jpg";
        vm.ActionType = "moveRename"; vm.RenameTemplate = "{unknown}";
        var errors = vm.Validate();
        Assert.Contains(errors, e => e.Contains("模板"));
    }

    [Fact]
    public void ApplyToModel_WritesRegexCondition()
    {
        var vm = new RuleEditorViewModel();
        vm.Name = "r"; vm.RegexPattern = @"^IMG"; vm.RegexCaseSensitive = true;
        vm.ApplyToModel();
        var c = Assert.IsType<RegexCondition>(vm.Model.Conditions[0]);
        Assert.Equal(@"^IMG", c.Pattern);
        Assert.False(c.IgnoreCase);
    }

    [Fact]
    public void ApplyToModel_WritesRenameAction()
    {
        var vm = new RuleEditorViewModel();
        vm.ActionType = "moveRename"; vm.RenameTemplate = "{name}{ext}";
        vm.ApplyToModel();
        var a = Assert.IsType<MoveAndRenameAction>(vm.Model.Actions[0]);
        Assert.Equal("{name}{ext}", a.Template);
    }

    [Fact]
    public void ApplyToModel_DefaultActionIsMove()
    {
        var vm = new RuleEditorViewModel();
        vm.ApplyToModel();
        Assert.IsType<MoveAction>(vm.Model.Actions[0]);
    }

    [Fact]
    public void ApplyToModel_InvalidTemplateKeepsRenameAction()
    {
        // 非法模板不得被静默降级为 MoveAction 且丢失模板文本
        var vm = new RuleEditorViewModel();
        vm.ActionType = "moveRename"; vm.RenameTemplate = "{unknown}";
        vm.ApplyToModel();
        var a = Assert.IsType<MoveAndRenameAction>(vm.Model.Actions[0]);
        Assert.Equal("{unknown}", a.Template);
    }

    [Fact]
    public void ApplyToModel_WhitespaceRegex_NotPersisted()
    {
        // 空白正则与校验口径一致：不写入条件（运行时永不命中）
        var vm = new RuleEditorViewModel();
        vm.RegexPattern = " ";
        vm.ApplyToModel();
        Assert.Empty(vm.Model.Conditions.OfType<RegexCondition>());
    }
}