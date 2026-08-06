// tests/FileTidy.Tests/RuleEditorViewModelTests.cs
using System.IO;
using FileTidy.App.ViewModels;
using FileTidy.Core.Models;

namespace FileTidy.Tests;

public class RuleEditorViewModelTests
{
    [Fact]
    public void Validation_InvalidRegexReportsError()
    {
        var vm = new RuleEditorViewModel();
        vm.Name = "r"; vm.SourcePath = Directory.CreateTempSubdirectory("ed").FullName;
        vm.TargetPath = Path.Combine(vm.SourcePath, "out"); vm.Extensions = "jpg";
        vm.RegexPattern = "(";
        var errors = vm.Validate();
        Assert.Contains(errors, e => e.Contains("正则"));
    }

    [Fact]
    public void Validation_InvalidTemplateReportsError()
    {
        var vm = new RuleEditorViewModel();
        vm.Name = "r"; vm.SourcePath = Directory.CreateTempSubdirectory("ed2").FullName;
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
}