// tests/FileTidy.Tests/MainViewModelTests.cs
using System.IO;
using FileTidy.App;
using FileTidy.App.ViewModels;

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
    public async Task TidyCommand_WithOneRule_MovesFile()
    {
        var srcDir = Path.Combine(_dir, "src"); var targetDir = Path.Combine(_dir, "target");
        Directory.CreateDirectory(srcDir); Directory.CreateDirectory(targetDir);
        File.WriteAllText(Path.Combine(srcDir, "a.jpg"), "x");

        var vm = new MainViewModel(new SettingsService(Path.Combine(_dir, "config.json")), coreTimeProvider: () => DateTime.Now);
        vm.AddRule();
        vm.SelectedEditor!.Name = "图片"; vm.SelectedEditor.SourcePath = srcDir; vm.SelectedEditor.TargetPath = targetDir;
        vm.SelectedEditor.AddExtension("jpg");
        vm.SelectedEditor.ApplyToModel();

        await vm.TidyCommand.ExecuteAsync();

        Assert.True(File.Exists(Path.Combine(targetDir, "a.jpg")));
        Assert.Contains("成功", vm.StatusText);
    }
}