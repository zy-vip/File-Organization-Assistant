using System.Collections.ObjectModel;
using FileTidy.App;
using FileTidy.Core;
using FileTidy.Core.Models;

namespace FileTidy.App.ViewModels;

/// <summary>预览表格行</summary>
public class PreviewRow
{
    public required string Source { get; init; }
    public string? Dest { get; init; }
    public required string StatusText { get; init; }
    public required bool Warned { get; init; }
}

/// <summary>主窗口 ViewModel</summary>
public class MainViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly Func<DateTime> _now;
    private readonly EngageQueue _queue = new();
    private readonly FolderWatcher _watcher = new();

    public ObservableCollection<Rule> Rules { get; } = new();
    public ObservableCollection<RuleEditorViewModel> EditorVms { get; } = new();
    public ObservableCollection<PreviewRow> PreviewRows { get; } = new();
    public string StatusText { get => _status; private set => SetProperty(ref _status, value); }
    public bool Busy { get => _busy; private set => SetProperty(ref _busy, value); }
    private string _status = "就绪"; private bool _busy;

    /// <summary>自动整理开关：变更即保存并刷新监听列表（Replace 在 Save 内统一处理）</summary>
    public bool AutoTidy
    {
        get => _auto;
        set
        {
            if (SetProperty(ref _auto, value)) Save();
        }
    }
    private bool _auto;

    /// <summary>操作日志保留份数（构造时从配置加载，默认 10）</summary>
    private int _retention = 10;

    public RelayCommand PreviewCommand { get; }
    public RelayCommand TidyCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand AddRuleCommand { get; }

    public RuleEditorViewModel? SelectedEditor
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }
    private RuleEditorViewModel? _selected;

    public MainViewModel(SettingsService settings, Func<DateTime>? coreTimeProvider = null)
    {
        _settings = settings;
        _now = coreTimeProvider ?? (() => DateTime.Now);
        PreviewCommand = new RelayCommand(PreviewAsync);
        TidyCommand = new RelayCommand(TidyAsync);
        UndoCommand = new RelayCommand(UndoAsync);
        AddRuleCommand = new RelayCommand(() => { AddRule(); return Task.CompletedTask; });
        // 事件订阅只做一次；开关状态由 AutoTidyAsync 内部检查，避免开启/关闭失效
        _watcher.TidyTriggered += () => _ = AutoTidyAsync();
        LoadConfig();
    }

    private void LoadConfig()
    {
        var config = _settings.Load();
        _retention = config.OperationLogRetention;
        // 先填充规则再置开关属性：开关 setter 会触发 Save()，避免空规则覆盖已存配置
        foreach (var rule in config.Rules)
        {
            Rules.Add(rule);
            Attach(FromRule(rule));
        }
        AutoTidy = config.AutoTidyEnabled;
        StartWithWindows = config.StartWithWindows;
        AutoRenameOnConflict = config.AutoRenameOnConflict;
        if (AutoTidy) _watcher.Watch(Rules.Select(r => r.SourcePath).ToArray());
    }

    /// <summary>订阅编辑器变更：规则编辑实时保存（配置实时落盘）</summary>
    private void Attach(RuleEditorViewModel vm)
        => vm.PropertyChanged += (_, _) => Save();

    private static RuleEditorViewModel FromRule(Rule rule) => new()
    {
        Model = rule,
        Name = rule.Name,
        SourcePath = rule.SourcePath,
        TargetPath = rule.TargetPath,
        IncludeSubfolders = rule.IncludeSubfolders,
        ExcludeTargetTree = rule.ExcludeTargetTree,
        AutoRenameOnConflict = rule.AutoRenameOnConflict,
        Extensions = string.Join(", ", rule.Conditions.OfType<ExtensionCondition>().SelectMany(c => c.Extensions)),
        Keywords = rule.Conditions.OfType<KeywordCondition>().Select(c => c.Keyword).FirstOrDefault() ?? "",
        AgeDays = rule.Conditions.OfType<AgeCondition>().Select(c => c.Days).FirstOrDefault()
    };

    /// <summary>新增规则并自动选中（新规则继承全局冲突序号开关）</summary>
    public void AddRule()
    {
        var vm = new RuleEditorViewModel { AutoRenameOnConflict = AutoRenameOnConflict };
        EditorVms.Add(vm);
        Rules.Add(vm.Model);
        SelectedEditor = vm;
        Attach(vm);
    }

    public void DeleteRule(RuleEditorViewModel vm)
    {
        EditorVms.Remove(vm);
        Rules.Remove(vm.Model);
        Save();
    }

    /// <summary>保存配置（规则编辑实时触发）；自动模式开启时同步刷新监听列表</summary>
    private void Save()
    {
        foreach (var vm in EditorVms) vm.ApplyToModel();
        _settings.Save(new FileTidyConfig
        {
            Rules = Rules.ToList(),
            AutoTidyEnabled = AutoTidy,
            AutoRenameOnConflict = AutoRenameOnConflict,
            OperationLogRetention = _retention,
            StartWithWindows = StartWithWindows
        });
        if (AutoTidy) _watcher.Replace(Rules.Select(r => r.SourcePath).ToArray());
    }

    /// <summary>全局冲突序号开关：作为新规则的默认值；规则级开关仍优先</summary>
    public bool AutoRenameOnConflict
    {
        get => _globalRename;
        set { if (SetProperty(ref _globalRename, value)) Save(); }
    }
    private bool _globalRename = true;

    /// <summary>开机自启开关：变更即写注册表并保存</summary>
    public bool StartWithWindows
    {
        get => _startup;
        set
        {
            if (SetProperty(ref _startup, value))
            {
                AutoStartService.SetEnabled(value);
                Save();
            }
        }
    }
    private bool _startup;

    /// <summary>整理失败/跳过明细（界面红字展示，失败原因列表）</summary>
    public string? ErrorDetails { get => _errorDetails; private set => SetProperty(ref _errorDetails, value); }
    private string? _errorDetails;

    /// <summary>是否有明细需要展示（供 XAML 可见性绑定）</summary>
    public bool HasErrorDetails => ErrorDetails is not null;

    private void SetErrorDetails(IReadOnlyList<OrganizeItem> failed, IReadOnlyList<OrganizeItem> skipped)
    {
        var lines = new List<string>();
        lines.AddRange(failed.Select(f => $"失败：{f.Source} → {f.Dest}：{f.Reason}"));
        lines.AddRange(skipped.Select(s => $"跳过：{s.Source}：{s.Reason}"));
        ErrorDetails = lines.Count > 0 ? string.Join("\n", lines) : null;
        OnPropertyChanged(nameof(HasErrorDetails));
    }

    private async Task PreviewAsync()
    {
        if (Busy) return;
        try
        {
            Busy = true; StatusText = "正在扫描…";
            await _queue.RunAsync(async () =>
            {
                await Task.Yield();
                BuildPreview();
                return true;
            });
            StatusText = $"预览完成，共 {PreviewRows.Count} 个文件";
        }
        catch (InvalidOperationException)
        {
            StatusText = "整理正在进行中，请稍候";
        }
        finally { Busy = false; }
    }

    private void BuildPreview()
    {
        var now = _now();
        PreviewRows.Clear();
        foreach (var vm in EditorVms) vm.ApplyToModel();
        var files = ScanAllSources();
        foreach (var p in PreviewService.Build(Rules.ToList(), files, now))
        {
            PreviewRows.Add(new PreviewRow
            {
                Source = p.File.FullPath,
                Dest = p.DestPath,
                StatusText = p.Status == PreviewStatus.Moved ? "将移动"
                           : p.Status == PreviewStatus.Conflict ? "冲突"
                           : "未命中",
                Warned = p.Status != PreviewStatus.Moved
            });
        }
    }

    /// <summary>合并所有规则的源文件（去重）——保证单轮不重复处理</summary>
    private List<FileEntry> ScanAllSources()
    {
        var files = new List<FileEntry>();
        foreach (var rule in Rules)
        {
            var excludeRoots = rule.ExcludeTargetTree ? new[] { rule.TargetPath } : Array.Empty<string>();
            files.AddRange(FileScanner.Scan(rule.SourcePath, rule.IncludeSubfolders, excludeRoots));
        }
        return files.DistinctBy(f => f.FullPath).ToList();
    }

    private async Task TidyAsync()
    {
        if (Busy) return;
        try
        {
            Busy = true;
            await _queue.RunAsync(async () =>
            {
                await Task.Yield();
                BuildPreview();
                var previews = CollectPreviews();
                if (previews.Count == 0) { StatusText = "没有需要整理的文件"; return false; }
                var (result, record) = Organizer.Execute(previews, _now());
                new OperationLog(AppPaths.OperationsDir, _retention).Save(record);
                SetErrorDetails(result.Failed, result.Skipped);
                StatusText = $"整理完成：成功 {result.Succeeded}，跳过 {result.Skipped.Count}，失败 {result.Failed.Count}";
                return true;
            });
        }
        catch (InvalidOperationException)
        {
            StatusText = "整理正在进行中，请稍候";
        }
        finally { Busy = false; }
    }

    private List<PreviewEntry> CollectPreviews()
        => PreviewService.Build(Rules.ToList(), ScanAllSources(), _now());

    private async Task UndoAsync()
    {
        if (Busy) return;
        try
        {
            Busy = true;
            await _queue.RunAsync(async () =>
            {
                await Task.Yield();
                var log = new OperationLog(AppPaths.OperationsDir, _retention);
                var record = log.Latest();
                if (record is null) { StatusText = "没有可撤销的操作"; return false; }
                var result = Organizer.Undo(record);
                log.DiscardLatest();
                SetErrorDetails(Array.Empty<OrganizeItem>(), result.Skipped);
                StatusText = $"已撤销：还原 {result.Restored}，跳过 {result.Skipped.Count}";
                return true;
            });
        }
        catch (InvalidOperationException)
        {
            StatusText = "整理正在进行中，请稍候";
        }
        finally { Busy = false; }
    }

    /// <summary>自动整理：开关关闭时忽略触发事件；完成后触发 TidyCompleted 供托盘通知</summary>
    public event Action<string>? TidyCompleted;

    private async Task AutoTidyAsync()
    {
        if (!AutoTidy) return;
        await Task.Delay(3000);
        try
        {
            await _queue.RunAsync(async () =>
            {
                await Task.Yield();
                var previews = CollectPreviews();
                if (previews.Count == 0) return false;
                var (result, record) = Organizer.Execute(previews, _now());
                new OperationLog(AppPaths.OperationsDir, _retention).Save(record);
                TidyCompleted?.Invoke($"自动整理完成：成功 {result.Succeeded}，跳过 {result.Skipped.Count}，失败 {result.Failed.Count}");
                return true;
            });
        }
        catch (InvalidOperationException) { }
    }

    /// <summary>应用退出前的收尾：保存配置并停止监听</summary>
    public void Shutdown()
    {
        Save();
        _watcher.Dispose();
    }
}