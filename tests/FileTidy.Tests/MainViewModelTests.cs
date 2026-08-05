// tests/FileTidy.Tests/MainViewModelTests.cs
using System.IO;
using FileTidy.App;
using FileTidy.App.ViewModels;
using FileTidy.Core.Models;

namespace FileTidy.Tests;

public class MainViewModelTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("vm").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void RuleEditor_Validation_ReportsErrors()
    {
        var vm = new RuleEditorViewModel();
        vm.SourcePath = ""; vm.TargetPath = ""; vm.Name = "";
        var errors = vm.Validate();
        Assert.Contains(errors, e => e.Contains("名称"));
        Assert.Contains(errors, e => e.Contains("源文件夹"));
        Assert.Contains(errors, e => e.Contains("目标文件夹"));
        Assert.Contains(errors, e => e.Contains("条件"));
    }

    [Fact]
    public void RuleEditor_Validation_RejectsTargetInsideSource()
    {
        var vm = new RuleEditorViewModel();
        vm.Name = "循环"; vm.SourcePath = _dir;
        vm.TargetPath = Path.Combine(_dir, "sub"); vm.Extensions = "jpg";
        var errors = vm.Validate();
        Assert.Contains(errors, e => e.Contains("循环"));
    }

    [Fact]
    public void RuleEditor_Validation_ErrorSummaryUpdatesOnPropertyChange()
    {
        var vm = new RuleEditorViewModel();
        vm.Name = "x"; vm.SourcePath = _dir;
        vm.TargetPath = Path.Combine(Directory.GetParent(_dir)!.FullName, "vm-target"); vm.Extensions = "jpg";
        Assert.Null(vm.ErrorSummary);
        vm.SourcePath = "";
        Assert.NotNull(vm.ErrorSummary);
        Assert.Contains("源文件夹", vm.ErrorSummary);
    }

    [Fact]
    public void RuleEditor_ApplyToModel_KeepsAllKeywords()
    {
        var vm = new RuleEditorViewModel();
        vm.Name = "多词"; vm.SourcePath = _dir; vm.TargetPath = Path.Combine(_dir, "t");
        vm.Keywords = "alpha, beta";
        vm.ApplyToModel();
        Assert.Equal(2, vm.Model.Conditions.OfType<KeywordCondition>().Count());
    }

    [Fact]
    public void RuleEditor_ApplyToModel_EmptyAgeText_DisablesAgeCondition()
    {
        var vm = new RuleEditorViewModel();
        vm.Name = "清空期限"; vm.SourcePath = _dir; vm.TargetPath = Path.Combine(_dir, "t");
        vm.AgeDays = ""; // 清空输入框后不应产生日期条件
        vm.ApplyToModel();
        Assert.Empty(vm.Model.Conditions.OfType<AgeCondition>());
        vm.AgeDays = "30";
        vm.ApplyToModel();
        Assert.Single(vm.Model.Conditions.OfType<AgeCondition>());
    }

    [Fact]
    public void RuleEditor_Validation_RejectsSourceInsideTarget()
    {
        var vm = new RuleEditorViewModel();
        vm.Name = "倒置"; vm.SourcePath = Path.Combine(_dir, "sub");
        vm.TargetPath = _dir; vm.Extensions = "jpg";
        var errors = vm.Validate();
        Assert.Contains(errors, e => e.Contains("排除"));
    }

    [Fact]
    public void RuleEditor_Validation_NoFalsePositiveForPrefixSibling()
    {
        // 源 C:\foo，目标 C:\foobar（同级前缀相似）不应误报循环
        var parent = Directory.GetParent(_dir)!.FullName;
        var vm = new RuleEditorViewModel();
        vm.Name = "x"; vm.SourcePath = Path.Combine(parent, "foo");
        vm.TargetPath = Path.Combine(parent, "foobar"); vm.Extensions = "jpg";
        var errors = vm.Validate();
        Assert.DoesNotContain(errors, e => e.Contains("循环"));
    }

    private MainViewModel NewVm(string operationsDir)
        => new(new SettingsService(Path.Combine(_dir, "config.json")), coreTimeProvider: () => DateTime.Now, operationsDir: operationsDir);

    [Fact]
    public async Task TidyCommand_WithOneRule_MovesFile()
    {
        var srcDir = Path.Combine(_dir, "src"); var targetDir = Path.Combine(_dir, "target");
        var opsDir = Path.Combine(_dir, "ops");
        Directory.CreateDirectory(srcDir); Directory.CreateDirectory(targetDir);
        File.WriteAllText(Path.Combine(srcDir, "a.jpg"), "x");

        var vm = NewVm(opsDir);
        vm.AddRule();
        vm.SelectedEditor!.Name = "图片"; vm.SelectedEditor.SourcePath = srcDir; vm.SelectedEditor.TargetPath = targetDir;
        vm.SelectedEditor.AddExtension("jpg");
        vm.SelectedEditor.ApplyToModel();

        await vm.TidyCommand.ExecuteAsync();

        Assert.True(File.Exists(Path.Combine(targetDir, "a.jpg")));
        Assert.Contains("成功", vm.StatusText);
        Assert.Single(Directory.GetFiles(opsDir, "*.json")); // 日志写入注入目录而非真实 AppData
    }

    [Fact]
    public async Task TidyCommand_NoMovedFiles_SavesNoLog()
    {
        var srcDir = Path.Combine(_dir, "src2"); var targetDir = Path.Combine(_dir, "target2");
        var opsDir = Path.Combine(_dir, "ops2");
        Directory.CreateDirectory(srcDir); Directory.CreateDirectory(targetDir);
        Directory.CreateDirectory(opsDir); // 显式创建，便于断言"无日志文件"
        File.WriteAllText(Path.Combine(srcDir, "a.jpg"), "x"); // 文件存在但规则不匹配

        var vm = NewVm(opsDir);
        vm.AddRule();
        vm.SelectedEditor!.Name = "图片"; vm.SelectedEditor.SourcePath = srcDir; vm.SelectedEditor.TargetPath = targetDir;
        vm.SelectedEditor.AddExtension("png"); // 只匹配 png，jpg 不命中

        await vm.TidyCommand.ExecuteAsync();

        Assert.Contains("没有需要整理的文件", vm.StatusText);
        Assert.Empty(Directory.GetFiles(opsDir, "*.json")); // 不落空日志
    }
}