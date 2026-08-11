// tests/FileTidy.Tests/MainViewModelTests.cs
using System.IO;
using FileTidy.App;
using FileTidy.App.ViewModels;
using FileTidy.Core;
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
        => new(new SettingsService(Path.Combine(_dir, "config.json")),
               coreTimeProvider: () => DateTime.Now,
               operationsDir: operationsDir);

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

    [Fact]
    public void LoadConfig_PopulatesEditorList()
    {
        // 回归：启动时已存规则必须出现在左侧规则列表（EditorVms），否则 UI 看不到已有规则
        var opsDir = Path.Combine(_dir, "ops");
        Directory.CreateDirectory(opsDir);
        var config = new FileTidyConfig { AutoTidyEnabled = true };
        config.Rules.Add(new Rule
        {
            Name = "旧规则",
            SourcePath = Path.Combine(_dir, "src"),
            TargetPath = Path.Combine(_dir, "target"),
            Conditions = { new ExtensionCondition { Extensions = { "pdf" } } }
        });
        new SettingsService(Path.Combine(_dir, "config.json")).Save(config);

        var vm = new MainViewModel(new SettingsService(Path.Combine(_dir, "config.json")),
            coreTimeProvider: () => DateTime.Now, operationsDir: opsDir);

        Assert.Single(vm.EditorVms);
        Assert.Equal("旧规则", vm.EditorVms[0].Name);
        Assert.Single(vm.EditorVms[0].Model.Conditions);
        Assert.Same(vm.EditorVms[0], vm.SelectedEditor);
    }

    [Fact]
    public async Task AutoTidy_WatchesSource_DropsNewFile_MovesIt()
    {
        var srcDir = Path.Combine(_dir, "src"); var targetDir = Path.Combine(_dir, "target");
        var opsDir = Path.Combine(_dir, "ops");
        Directory.CreateDirectory(srcDir); Directory.CreateDirectory(targetDir);

        var vm = NewVm(opsDir);
        vm.AddRule();
        vm.SelectedEditor!.Name = "PDF"; vm.SelectedEditor.SourcePath = srcDir; vm.SelectedEditor.TargetPath = targetDir;
        vm.SelectedEditor.AddExtension("pdf");
        vm.AutoTidy = true; // 开启后注册监听

        var done = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.TidyCompleted += msg => done.TrySetResult(msg);

        File.WriteAllText(Path.Combine(srcDir, "新文件.pdf"), "x");

        var msg = await done.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Contains("成功", msg);
        Assert.True(File.Exists(Path.Combine(targetDir, "新文件.pdf")));
        Assert.Single(Directory.GetFiles(opsDir, "*.json")); // 自动整理同样写入日志
    }

    [Fact]
    public async Task Tidy_RenameSequenceMatchesPreview()
    {
        var dir = Directory.CreateTempSubdirectory("vm5").FullName;
        try
        {
            var srcDir = Path.Combine(dir, "src"); var targetDir = Path.Combine(dir, "target");
            Directory.CreateDirectory(srcDir); Directory.CreateDirectory(targetDir);
            File.WriteAllText(Path.Combine(srcDir, "a.jpg"), "x");
            File.WriteAllText(Path.Combine(srcDir, "b.jpg"), "x");

            var vm = new MainViewModel(new SettingsService(Path.Combine(dir, "config.json")));
            vm.Rules.Add(new Rule
            {
                Name = "重命名", SourcePath = srcDir, TargetPath = targetDir,
                Conditions = { new ExtensionCondition { Extensions = { "jpg" } } },
                Actions = { new MoveAndRenameAction { Template = "图{n}{ext}" } }
            });

            await vm.TidyCommand.ExecuteAsync();
            Assert.True(File.Exists(Path.Combine(targetDir, "图1.jpg")));
            Assert.True(File.Exists(Path.Combine(targetDir, "图2.jpg")));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Tidy_TemplateErrorOnly_StillRunsAndReportsFailure()
    {
        // 回归：仅有模板错误条目时不得早退，须执行整理并展示失败计数（本改动核心语义）
        var dir = Directory.CreateTempSubdirectory("vmTpl").FullName;
        try
        {
            var srcDir = Path.Combine(dir, "src"); var targetDir = Path.Combine(dir, "target");
            var opsDir = Path.Combine(dir, "ops");
            Directory.CreateDirectory(srcDir); Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(opsDir); // 显式创建，便于断言"无日志文件"
            File.WriteAllText(Path.Combine(srcDir, "a.jpg"), "x");

            var vm = new MainViewModel(new SettingsService(Path.Combine(dir, "config.json")),
                coreTimeProvider: () => DateTime.Now, operationsDir: opsDir);
            vm.Rules.Add(new Rule
            {
                Name = "模板缺组", SourcePath = srcDir, TargetPath = targetDir,
                Conditions = { new RegexCondition { Pattern = "jpg" } }, // 无捕获组
                Actions = { new MoveAndRenameAction { Template = "{1}{ext}" } } // 引用缺失捕获组 → 预览 TemplateError
            });

            await vm.TidyCommand.ExecuteAsync();

            Assert.Contains("失败 1", vm.StatusText); // 整理仍执行，未走"没有需要整理的文件"早退
            Assert.Contains("模板错误", vm.ErrorDetails);
            Assert.Empty(Directory.GetFiles(opsDir, "*.json")); // TemplateError 只计失败、不产生日志条目，不落空日志
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void MoveRule_ReordersAndPersists()
    {
        var dir = Directory.CreateTempSubdirectory("vm3").FullName;
        try
        {
            var vm = new MainViewModel(new SettingsService(Path.Combine(dir, "config.json")));
            vm.AddRule(); vm.SelectedEditor!.Name = "A";
            vm.AddRule(); vm.SelectedEditor!.Name = "B";
            vm.MoveRule(1, -1);
            Assert.Equal("B", vm.EditorVms[0].Name);
            Assert.Equal("A", vm.EditorVms[1].Name);
            Assert.Equal("B", vm.Rules[0].Name);
            var reloaded = new MainViewModel(new SettingsService(Path.Combine(dir, "config.json")));
            Assert.Equal("B", reloaded.Rules[0].Name);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Undo_MissingTarget_ReportsSkippedInStatusAndDetails()
    {
        // 回归：VM 层 skipped 报告链路（StatusText 计数 + ErrorDetails 跳过行）。
        // 整理路径的 skipped（预览后文件被删）在 TidyAsync 中预览与执行原子进行、无法确定性构造，
        // 故经撤销路径触发同一报告机制：整理成功 → 删除目标 → 撤销时文件缺失即 Skipped。
        var srcDir = Path.Combine(_dir, "src"); var targetDir = Path.Combine(_dir, "target");
        var opsDir = Path.Combine(_dir, "ops");
        Directory.CreateDirectory(srcDir); Directory.CreateDirectory(targetDir);
        File.WriteAllText(Path.Combine(srcDir, "a.jpg"), "x");

        var vm = NewVm(opsDir);
        vm.AddRule();
        vm.SelectedEditor!.Name = "图片"; vm.SelectedEditor.SourcePath = srcDir; vm.SelectedEditor.TargetPath = targetDir;
        vm.SelectedEditor.AddExtension("jpg");
        await vm.TidyCommand.ExecuteAsync();
        Assert.True(File.Exists(Path.Combine(targetDir, "a.jpg")));

        File.Delete(Path.Combine(targetDir, "a.jpg")); // 模拟目标文件已被外部删除
        await vm.UndoCommand.ExecuteAsync();

        Assert.Contains("跳过 1", vm.StatusText);
        Assert.Contains("跳过：", vm.ErrorDetails);
    }

    [Fact]
    public async Task Preview_PopulatesStatusEnum()
    {
        var dir = Directory.CreateTempSubdirectory("vmPrev").FullName;
        try
        {
            var src = Path.Combine(dir, "src"); var target = Path.Combine(dir, "target");
            Directory.CreateDirectory(src); Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(src, "a.jpg"), "x");

            var vm = NewVm(Path.Combine(dir, "ops"));
            vm.AddRule();
            vm.SelectedEditor!.Name = "图片"; vm.SelectedEditor.SourcePath = src; vm.SelectedEditor.TargetPath = target;
            vm.SelectedEditor.AddExtension("jpg");

            await vm.PreviewCommand.ExecuteAsync();

            Assert.Single(vm.PreviewRows);
            Assert.Equal(PreviewStatus.Moved, vm.PreviewRows[0].Status);
            Assert.False(vm.PreviewRows[0].Warned);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task EditDebounce_RepeatedTyping_SavesOnceAfterIdle()
    {
        var configPath = Path.Combine(_dir, "config.json");
        new SettingsService(configPath).Save(new FileTidyConfig()); // 预置空配置，保证防抖断言时文件已存在
        var vm = NewVm(Path.Combine(_dir, "ops"));
        vm.AddRule();
        vm.SelectedEditor!.Name = "规则";

        vm.SelectedEditor.Name = "规则一";   // 连续输入触发防抖
        vm.SelectedEditor.Name = "规则一二";
        Assert.DoesNotContain("规则一二", File.ReadAllText(configPath)); // 防抖窗口内未落盘

        await Task.Delay(1200);             // 等待防抖窗口 + 落盘

        var reloaded = new MainViewModel(new SettingsService(configPath));
        Assert.Equal("规则一二", reloaded.Rules[0].Name);
    }
}