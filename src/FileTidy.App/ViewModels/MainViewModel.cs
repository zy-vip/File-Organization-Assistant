using System.Collections.ObjectModel;
using System.Threading;
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
    public required PreviewStatus Status { get; init; }
}

/// <summary>主窗口 ViewModel</summary>
public class MainViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly Func<DateTime> _now;
    private readonly string _operationsDir;
    private readonly EngageQueue _queue = new();
    private readonly FolderWatcher _watcher = new();

    public ObservableCollection<Rule> Rules { get; } = new();
    public ObservableCollection<RuleEditorViewModel> EditorVms { get; } = new();
    public ObservableCollection<PreviewRow> PreviewRows { get; } = new();
    public string StatusText { get => _status; private set => SetProperty(ref _status, value); }
    public bool Busy { get => _busy; private set => SetProperty(ref _busy, value); }
    private string _status = "就绪"; private bool _busy;

    /// <summary>自动整理开关：变更即保存并增量同步监听列表</summary>
    public bool AutoTidy
    {
        get => _auto;
        set
        {
            if (SetProperty(ref _auto, value)) SaveNow();
        }
    }
    private bool _auto;

    /// <summary>操作日志保留份数（构造时从配置加载，默认 10）</summary>
    private int _retention = 10;

    public RelayCommand PreviewCommand { get; }
    public RelayCommand TidyCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand AddRuleCommand { get; }
    public RelayCommand DeleteRuleCommand { get; }
    public RelayCommand MoveRuleUpCommand { get; }
    public RelayCommand MoveRuleDownCommand { get; }

    public RuleEditorViewModel? SelectedEditor
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }
    private RuleEditorViewModel? _selected;

    public MainViewModel(SettingsService settings, Func<DateTime>? coreTimeProvider = null, string? operationsDir = null)
    {
        _settings = settings;
        _now = coreTimeProvider ?? (() => DateTime.Now);
        _operationsDir = operationsDir ?? AppPaths.OperationsDir;
        PreviewCommand = new RelayCommand(PreviewAsync);
        TidyCommand = new RelayCommand(TidyAsync);
        UndoCommand = new RelayCommand(UndoAsync);
        AddRuleCommand = new RelayCommand(() => { AddRule(); return Task.CompletedTask; });
        DeleteRuleCommand = new RelayCommand(() =>
        {
            if (SelectedEditor is not null) DeleteRule(SelectedEditor);
            return Task.CompletedTask;
        });
        MoveRuleUpCommand = new RelayCommand(() => { if (SelectedEditor is not null) MoveRule(EditorVms.IndexOf(SelectedEditor), -1); return Task.CompletedTask; });
        MoveRuleDownCommand = new RelayCommand(() => { if (SelectedEditor is not null) MoveRule(EditorVms.IndexOf(SelectedEditor), 1); return Task.CompletedTask; });
        // 事件订阅只做一次；开关状态由 AutoTidyAsync 内部检查，避免开启/关闭失效
        _watcher.TidyTriggered += () => _ = AutoTidyAsync();
        LoadConfig();
    }

    private void LoadConfig()
    {
        var config = _settings.Load();
        _retention = config.OperationLogRetention;
        // 先填充规则再置开关属性：开关 setter 会触发 SaveNow()，避免空规则覆盖已存配置
        foreach (var rule in config.Rules)
        {
            Rules.Add(rule);
            var vm = FromRule(rule);
            Attach(vm);
            EditorVms.Add(vm); // 已存规则同样出现在左侧列表
        }
        if (EditorVms.Count > 0) SelectedEditor = EditorVms[0];
        AutoTidy = config.AutoTidyEnabled;
        StartWithWindows = config.StartWithWindows;
        AutoRenameOnConflict = config.AutoRenameOnConflict;
        if (AutoTidy) _watcher.Watch(Rules.Select(r => r.SourcePath).ToArray());
    }

    /// <summary>订阅编辑器变更：编辑器文本输入走防抖保存（500ms 窗口内只写一次盘）</summary>
    private void Attach(RuleEditorViewModel vm)
        => vm.PropertyChanged += (_, _) => DebouncedSave();

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
        Keywords = string.Join(", ", rule.Conditions.OfType<KeywordCondition>().Select(c => c.Keyword)),
        AgeDays = rule.Conditions.OfType<AgeCondition>().Select(c => c.Days).FirstOrDefault().ToString(),
        RegexPattern = rule.Conditions.OfType<RegexCondition>().Select(c => c.Pattern).FirstOrDefault() ?? "",
        RegexCaseSensitive = rule.Conditions.OfType<RegexCondition>().Select(c => !c.IgnoreCase).FirstOrDefault(),
        ActionType = rule.Actions.OfType<MoveAndRenameAction>().Any() ? "moveRename" : "move",
        RenameTemplate = rule.Actions.OfType<MoveAndRenameAction>().Select(a => a.Template).FirstOrDefault() ?? ""
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
        SaveNow();
    }

    /// <summary>移动规则：delta 为 -1/+1；EditorVms 与 Rules 同步重排并保存</summary>
    public void MoveRule(int index, int delta)
    {
        var newIndex = index + delta;
        if (index < 0 || newIndex < 0 || newIndex >= EditorVms.Count) return;
        EditorVms.Move(index, newIndex);
        Rules.Move(index, newIndex);
        SaveNow();
    }

    private const int SaveDebounceMs = 500;
    private CancellationTokenSource? _saveCts;

    /// <summary>结构操作立即落盘（规则增删、排序、开关切换、加载）；AddRule 除外——原行为不保存，靠后续编辑触发</summary>
    private void SaveNow()
    {
        _saveCts?.Cancel();
        ApplyAndSave();
        SyncWatchers();
    }

    /// <summary>编辑器输入防抖落盘：500ms 内连续变更只写一次</summary>
    private void DebouncedSave()
    {
        _saveCts?.Cancel();
        var cts = _saveCts = new CancellationTokenSource();
        _ = DebouncedSaveAsync(cts.Token);
    }

    private async Task DebouncedSaveAsync(CancellationToken token)
    {
        try { await Task.Delay(SaveDebounceMs, token); }
        catch (OperationCanceledException) { return; }
        // 落盘与监听同步不得抛出未观察异常：失败时给出状态提示，避免配置丢失且用户零感知
        try
        {
            ApplyAndSave();
            SyncWatchers();
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            StatusText = "保存失败：config 配置写入错误，请检查磁盘空间或权限";
        }
    }

    private void ApplyAndSave()
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
    }

    /// <summary>自动模式开启时增量同步监听列表（区别于 Replace：不重建现存 watcher）</summary>
    private void SyncWatchers()
    {
        if (AutoTidy) _watcher.Sync(Rules.Select(r => r.SourcePath).ToArray());
    }

    /// <summary>全局冲突序号开关：作为新规则的默认值；规则级开关仍优先</summary>
    public bool AutoRenameOnConflict
    {
        get => _globalRename;
        set { if (SetProperty(ref _globalRename, value)) SaveNow(); }
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
                SaveNow();
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
            List<PreviewEntry>? previews = null;
            await _queue.RunAsync(async () =>
            {
                previews = await Task.Run(() => BuildPreview(render: false)); // 扫描在后台线程
                RenderPreviews(previews);                                     // 集合更新回调用上下文（UI）
                return true;
            });
            SetErrorDetails(Array.Empty<OrganizeItem>(), Array.Empty<OrganizeItem>());
            StatusText = $"预览完成，共 {previews?.Count ?? 0} 个文件";
        }
        catch (InvalidOperationException)
        {
            StatusText = "整理正在进行中，请稍候";
        }
        catch (Exception ex)
        {
            StatusText = "预览失败";
            SetErrorDetails(new[] { new OrganizeItem { Source = "", Reason = ex.Message } }, Array.Empty<OrganizeItem>());
        }
        finally { Busy = false; }
    }

    /// <summary>构建预览并填充表格（render=true 时刷新 PreviewRows；自动整理走后台线程，仅计算不渲染避免跨线程改 UI 集合）</summary>
    private List<PreviewEntry> BuildPreview(bool render = true)
    {
        var now = _now();
        foreach (var vm in EditorVms) vm.ApplyToModel();
        var files = ScanAllSources();
        var previews = PreviewService.Build(Rules.ToList(), files, now);
        if (render) RenderPreviews(previews);
        return previews;
    }

    /// <summary>把预览结果填入表格（必须在 UI 线程调用）</summary>
    private void RenderPreviews(List<PreviewEntry> previews)
    {
        PreviewRows.Clear();
        foreach (var p in previews)
        {
            PreviewRows.Add(new PreviewRow
            {
                Source = p.File.FullPath,
                Dest = p.DestPath,
                StatusText = p.Status == PreviewStatus.Moved ? "将移动"
                           : p.Status == PreviewStatus.Conflict ? "冲突"
                           : p.Status == PreviewStatus.TemplateError ? "模板错误"
                           : "未命中",
                Warned = p.Status != PreviewStatus.Moved,
                Status = p.Status
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
                var outcome = await Task.Run(() =>
                {
                    var previews = BuildPreview(render: false);
                    // 传完整批次给 Organizer：TemplateError 计失败、Moved 执行移动，其余（未命中/冲突）自动忽略
                    var movable = previews.Where(p => p.Status == PreviewStatus.Moved).ToList();
                    var templateErrors = previews.Where(p => p.Status == PreviewStatus.TemplateError).ToList();
                    if (movable.Count == 0 && templateErrors.Count == 0)
                        return (Result: (OrganizeResult?)null, Failed: (List<OrganizeItem>?)null,
                                Skipped: (List<OrganizeItem>?)null, Previews: previews, Handled: false);
                    var (result, record) = Organizer.Execute(previews, _now());
                    if (record.Entries.Count > 0) new OperationLog(_operationsDir, _retention).Save(record);
                    return (Result: result, Failed: result.Failed, Skipped: result.Skipped, Previews: previews, Handled: true);
                });
                RenderPreviews(outcome.Previews!); // 恢复旧行为：整理时同步刷新预览表格（早退分支同样刷新）
                if (!outcome.Handled)
                {
                    StatusText = "没有需要整理的文件";
                    return false; // 无任何可执行/可报告条目，不落空日志；ErrorDetails 不动（与旧行为一致）
                }
                SetErrorDetails(outcome.Failed!, outcome.Skipped!);
                StatusText = $"整理完成：成功 {outcome.Result!.Succeeded}，跳过 {outcome.Result.Skipped.Count}，失败 {outcome.Result.Failed.Count}";
                return true;
            });
        }
        catch (InvalidOperationException)
        {
            StatusText = "整理正在进行中，请稍候";
        }
        catch (Exception ex)
        {
            StatusText = "整理失败";
            SetErrorDetails(new[] { new OrganizeItem { Source = "", Reason = ex.Message } }, Array.Empty<OrganizeItem>());
        }
        finally { Busy = false; }
    }

    private async Task UndoAsync()
    {
        if (Busy) return;
        try
        {
            Busy = true;
            await _queue.RunAsync(async () =>
            {
                var outcome = await Task.Run(() =>
                {
                    var log = new OperationLog(_operationsDir, _retention);
                    var record = log.Latest();
                    if (record is null) return (Result: (Organizer.UndoResult?)null, Found: false);
                    var result = Organizer.Undo(record);
                    log.DiscardLatest();
                    return (Result: result, Found: true);
                });
                if (!outcome.Found) { StatusText = "没有可撤销的操作"; return false; }
                SetErrorDetails(Array.Empty<OrganizeItem>(), outcome.Result!.Skipped);
                StatusText = $"已撤销：还原 {outcome.Result.Restored}，跳过 {outcome.Result.Skipped.Count}";
                return true;
            });
        }
        catch (InvalidOperationException)
        {
            StatusText = "整理正在进行中，请稍候";
        }
        catch (Exception ex)
        {
            StatusText = "撤销失败";
            SetErrorDetails(new[] { new OrganizeItem { Source = "", Reason = ex.Message } }, Array.Empty<OrganizeItem>());
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
                var previews = BuildPreview(render: false);
                // 传完整批次：TemplateError 计入失败统计；但无 Moved 文件时直接早退（静默，
                // 不触发 TidyCompleted，与手动模式会报告失败不对称——既有语义，保持不重构）
                var movable = previews.Where(p => p.Status == PreviewStatus.Moved).ToList();
                if (movable.Count == 0) return false;
                var (result, record) = Organizer.Execute(previews, _now());
                new OperationLog(_operationsDir, _retention).Save(record);
                TidyCompleted?.Invoke($"自动整理完成：成功 {result.Succeeded}，跳过 {result.Skipped.Count}，失败 {result.Failed.Count}");
                return true;
            });
        }
        catch (InvalidOperationException) { }
        catch (Exception ex)
        {
            TidyCompleted?.Invoke($"自动整理失败：{ex.Message}");
        }
    }

    private bool _shutdown;

    /// <summary>应用退出前的收尾：保存配置并停止监听（幂等，可被窗口关闭与退出流程重复调用）</summary>
    public void Shutdown()
    {
        if (_shutdown) return;
        _shutdown = true;
        SaveNow();
        _watcher.Dispose();
    }
}