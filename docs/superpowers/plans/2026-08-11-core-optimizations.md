# FileTidy 核心优化（10 项）实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 完成 10 项代码优化：手动整理后台化、单实例、保存防抖、watcher 增量同步、托盘图标、事件重试、watcher 重挂、日志 camelCase、命令样板抽取、具名常量。

**Architecture:** 全部为既有代码的增量改进，不新增业务能力。改动集中在 Core（FolderWatcher/OperationLog/RegexCondition）与 App（MainViewModel/TrayIcon/App/MainWindow.xaml.cs）。每项保持既有行为语义，测试全绿保护回归。

**Tech Stack:** C# / .NET 8 / WPF / xUnit。

## Global Constraints

- 代码注释与提交信息一律简体中文；提交用 conventional 风格。
- 不得用 PowerShell 的 `Set-Content`/`Out-File` 写含中文的文件（破坏 UTF-8），文件修改必须用编辑器工具。
- 测试用真实临时目录（`Directory.CreateTempSubdirectory`）；跨卷测试仅 Windows（已有 subst 用例）。
- 配置 `%AppData%\FileTidy\config.json` camelCase；操作日志当前 PascalCase（Task 8 统一并保持旧日志可读）。
- 不新增 NuGet 依赖；Core 保持零依赖。
- 现有 101 个测试必须全部保持通过。
- MainViewModel 构造签名不得变更（`MainViewModel(SettingsService, Func<DateTime>? = null, string? = null)`），测试依赖。
- 工作目录：`D:\demo\File-Organization-Assistant`；测试命令：`dotnet test tests/FileTidy.Tests`。

---

### Task 1: 清理 LicenseTool 私钥残留

**Files:**
- Delete: `tools/FileTidy.LicenseTool/private_key.pem`（及空目录 `tools/`）

> 注意：`*.pem` 已在 `.gitignore`，该文件**未被 git 跟踪**，删除后仓库无任何变更，**无需提交**。

- [ ] **Step 1: 删除残留文件**

```powershell
Remove-Item -LiteralPath "tools\FileTidy.LicenseTool\private_key.pem" -Force
Remove-Item -LiteralPath "tools\FileTidy.LicenseTool" -Force
Remove-Item -LiteralPath "tools" -Force
```

- [ ] **Step 2: 确认无残留**

```powershell
Test-Path -LiteralPath "tools"
git status --short   # 应无变更（文件未跟踪）
```

---

### Task 2: 单实例守护

**Files:**
- Create: `src/FileTidy.App/SingleInstanceGuard.cs`
- Modify: `src/FileTidy.App/App.xaml.cs`（OnStartup 集成）
- Test: `tests/FileTidy.Tests/SingleInstanceGuardTests.cs`

**Interfaces:**
- Produces: `SingleInstanceGuard(string name)`；`bool IsFirstInstance { get; }`；`void Dispose()`。同名字符串的第二个实例 `IsFirstInstance == false`，释放后第三个实例再次为 `true`。

- [ ] **Step 1: 写失败测试**

```csharp
// tests/FileTidy.Tests/SingleInstanceGuardTests.cs
using FileTidy.App;

namespace FileTidy.Tests;

public class SingleInstanceGuardTests
{
    private const string Name = "FileTidy.Test.SingleInstance";

    [Fact]
    public void FirstInstance_Wins_SecondIsRejected_ReleasedAllowsNext()
    {
        var first = new SingleInstanceGuard(Name);
        try
        {
            Assert.True(first.IsFirstInstance);
            using var second = new SingleInstanceGuard(Name);
            Assert.False(second.IsFirstInstance);
        }
        finally { first.Dispose(); }

        using var third = new SingleInstanceGuard(Name);
        Assert.True(third.IsFirstInstance);
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/FileTidy.Tests --filter FullyQualifiedName~SingleInstanceGuard` — Expected: 编译失败（类型不存在）

- [ ] **Step 3: 实现**

```csharp
// src/FileTidy.App/SingleInstanceGuard.cs
using System.Threading;

namespace FileTidy.App;

/// <summary>单实例守护：以命名互斥体保证同一用户会话内只有一个实例；非首例 Dispose 时不持有互斥体</summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex? _mutex;

    /// <summary>当前进程是否为第一个实例</summary>
    public bool IsFirstInstance { get; }

    public SingleInstanceGuard(string name)
    {
        _mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        IsFirstInstance = createdNew;
        if (!IsFirstInstance)
        {
            _mutex.Dispose();
            _mutex = null;
        }
    }

    public void Dispose() => _mutex?.Dispose();
}
```

- [ ] **Step 4: App.xaml.cs 集成（非首例提示并退出）**

```csharp
// App.xaml.cs 内新增字段
private SingleInstanceGuard? _guard;

protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    _guard = new SingleInstanceGuard("FileTidy.SingleInstance");
    if (!_guard.IsFirstInstance)
    {
        MessageBox.Show("FileTidy 已在运行，请从系统托盘打开主界面。", "文件整理助手",
            MessageBoxButton.OK, MessageBoxImage.Information);
        Shutdown();
        return;
    }
    AppPaths.EnsureCreated();
    // ... 其余保持原样
}
```

- [ ] **Step 5: 测试通过**

Run: `dotnet test tests/FileTidy.Tests --filter FullyQualifiedName~SingleInstanceGuard` — Expected: PASS

- [ ] **Step 6: 提交**

```bash
git add src/FileTidy.App/SingleInstanceGuard.cs src/FileTidy.App/App.xaml.cs tests/FileTidy.Tests/SingleInstanceGuardTests.cs
git commit -m "feat: 增加单实例守护，避免多开并发整理冲突"
```

---

### Task 3: 托盘图标与异步调度

**Files:**
- Modify: `src/FileTidy.App/TrayIcon.cs`

- [ ] **Step 1: 修改 TrayIcon（设置图标 + BeginInvoke）**

```csharp
// src/FileTidy.App/TrayIcon.cs 构造函数
_icon = new TaskbarIcon
{
    Icon = LoadIcon(),
    ToolTipText = "文件整理助手",
    ContextMenu = BuildMenu()
};
_icon.TrayMouseDoubleClick += (_, _) => ShowWindow();
// 自动整理完成 → 托盘通知（事件来自线程池，异步调度回 UI 线程）
vm.TidyCompleted += msg => Application.Current.Dispatcher.BeginInvoke(() =>
    _icon.ShowBalloonTip("文件整理助手", msg, BalloonIcon.Info));

/// <summary>从当前可执行文件提取图标；失败时返回 null（不阻塞启动）</summary>
private static System.Drawing.Icon? LoadIcon()
{
    try
    {
        return System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "");
    }
    catch
    {
        return null;
    }
}
```

- [ ] **Step 2: 构建验证**

Run: `dotnet build --nologo -v q` — Expected: 0 错误（`Environment.ProcessPath` 需 net6+，当前 net8.0-windows OK）

> **风险预案**：若 `System.Drawing.Icon` 在 WPF 共享框架不可用（编译失败），回退方案——从 `FileTidy.ico` 文件加载：
> `System.Drawing.Icon.ExtractAssociatedIcon` 改为 `new System.Drawing.Icon(AppPaths.ExePath 所在目录 + "FileTidy.ico")` 或直接不设置 Icon 保持现状并说明原因。

- [ ] **Step 3: 提交**

```bash
git add src/FileTidy.App/TrayIcon.cs
git commit -m "fix: 托盘设置程序图标并以异步调度刷新通知，避免阻塞事件线程"
```

---

### Task 4: 手动预览/整理/撤销后台化

**Files:**
- Modify: `src/FileTidy.App/ViewModels/MainViewModel.cs`（PreviewAsync/TidyAsync/UndoAsync）

**原则：** IO（扫描/移动/日志读写）放 `Task.Run` 后台；集合更新（RenderPreviews）、StatusText、ErrorDetails 留在队列任务内（调用方上下文，测试与 UI 均成立）。`EngageQueue` 的忙位在 `await` 期间保持（内部 `_busy` 在 work 完成前不清除），串行语义不变。

- [ ] **Step 1: 先跑既有测试建立基线**

Run: `dotnet test tests/FileTidy.Tests` — Expected: 101 PASS

- [ ] **Step 2: PreviewAsync 改造**

```csharp
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
    catch (InvalidOperationException) { StatusText = "整理正在进行中，请稍候"; }
    catch (Exception ex)
    {
        StatusText = "预览失败";
        SetErrorDetails(new[] { new OrganizeItem { Source = "", Reason = ex.Message } }, Array.Empty<OrganizeItem>());
    }
    finally { Busy = false; }
}
```

- [ ] **Step 3: TidyAsync 改造（IO 后台、UI 更新回上下文）**

> 关键：lambda 两个分支的返回元组必须**类型一致**，统一为命名元组 `(OrganizeResult? Result, List<OrganizeItem>? Failed, List<OrganizeItem>? Skipped, bool Handled)`。

```csharp
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
                            Skipped: (List<OrganizeItem>?)null, Handled: false);
                var (result, record) = Organizer.Execute(previews, _now());
                if (record.Entries.Count > 0) new OperationLog(_operationsDir, _retention).Save(record);
                return (Result: result, Failed: result.Failed, Skipped: result.Skipped, Handled: true);
            });
            if (!outcome.Handled)
            {
                StatusText = "没有需要整理的文件";
                return false; // 无任何可执行/可报告条目，不落空日志
            }
            SetErrorDetails(outcome.Failed!, outcome.Skipped!);
            StatusText = $"整理完成：成功 {outcome.Result!.Succeeded}，跳过 {outcome.Result.Skipped.Count}，失败 {outcome.Result.Failed.Count}";
            return true;
        });
    }
    catch (InvalidOperationException) { StatusText = "整理正在进行中，请稍候"; }
    catch (Exception ex)
    {
        StatusText = "整理失败";
        SetErrorDetails(new[] { new OrganizeItem { Source = "", Reason = ex.Message } }, Array.Empty<OrganizeItem>());
    }
    finally { Busy = false; }
}
```

- [ ] **Step 4: UndoAsync 改造（元组统一为 `(Organizer.UndoResult? Result, bool Found)`）**

```csharp
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
    catch (InvalidOperationException) { StatusText = "整理正在进行中，请稍候"; }
    catch (Exception ex)
    {
        StatusText = "撤销失败";
        SetErrorDetails(new[] { new OrganizeItem { Source = "", Reason = ex.Message } }, Array.Empty<OrganizeItem>());
    }
    finally { Busy = false; }
}
```

- [ ] **Step 5: 全量测试**

Run: `dotnet test tests/FileTidy.Tests` — Expected: 101 PASS（含 AutoTidy 端到端、撤销跳过报告用例）

- [ ] **Step 6: 提交**

```bash
git add src/FileTidy.App/ViewModels/MainViewModel.cs
git commit -m "perf: 手动预览/整理/撤销的 IO 移至后台线程，避免大目录整理冻结界面"
```

---

### Task 5: 保存防抖 + watcher 增量同步

**Files:**
- Modify: `src/FileTidy.Core/FolderWatcher.cs`（新增 `Sync`，内部提取 `AddWatcher`）
- Modify: `src/FileTidy.App/ViewModels/MainViewModel.cs`（Save 拆分：立即保存 / 防抖保存；Sync 替换 Replace）
- Test: `tests/FileTidy.Tests/FolderWatcherTests.cs`（补 Sync 用例）
- Test: `tests/FileTidy.Tests/MainViewModelTests.cs`（补防抖用例）

**语义约定（防回归）：**
- 结构操作（AddRule/DeleteRule/MoveRule/开关 setter/LoadConfig）→ `SaveNow()` 立即落盘（MoveRule_ReordersAndPersists、AutoTidy 端到端测试依赖即时性）。
- 编辑器文本输入（Attach 的 PropertyChanged）→ `DebouncedSave()`：500ms 内连续输入只写一次盘。
- 保存后统一 `SyncWatchers()`：仅对新增源目录建监听、对已移除源目录停监听，不重建现存 watcher。

- [ ] **Step 1: 写失败测试（FolderWatcher.Sync）**

> 目录先用临时目录模式（同 Replace_SwitchesToNewFolders 风格）；`c` 必须在 `Sync` 之前创建（Sync 只监听已存在的目录）。

```csharp
// tests/FileTidy.Tests/FolderWatcherTests.cs 追加
[Fact]
public async Task Sync_AddsNew_RemovesGone_KeepsExisting()
{
    var dir2 = Path.Combine(Path.GetTempPath(), "watch-sync-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir2);
    try
    {
        var a = Path.Combine(_dir, "a"); var b = Path.Combine(_dir, "b");
        Directory.CreateDirectory(a); Directory.CreateDirectory(b);
        Directory.CreateDirectory(dir2); // Sync 只监听已存在目录，c 必须提前创建

        _watcher = new FolderWatcher();
        _watcher.Watch(new[] { a, b });

        var count = 0;
        _watcher.TidyTriggered += () => count++;
        _watcher.Sync(new[] { b, dir2 }); // b 保留、a 移除、dir2 新增

        File.WriteAllText(Path.Combine(dir2, "new.txt"), "x");
        await Task.Delay(500);
        var afterNew = count;
        Assert.True(afterNew > 0, "新增目录应被监听");

        File.WriteAllText(Path.Combine(a, "old.txt"), "x");
        await Task.Delay(500);
        Assert.Equal(afterNew, count); // 已移除目录不再触发
    }
    finally { Directory.Delete(dir2, true); }
}
```

- [ ] **Step 2: 写失败测试（防抖保存）**

```csharp
// tests/FileTidy.Tests/MainViewModelTests.cs 追加
[Fact]
public async Task EditDebounce_RepeatedTyping_SavesOnceAfterIdle()
{
    var configPath = Path.Combine(_dir, "config.json");
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
```

- [ ] **Step 3: 运行确认失败**

Run: `dotnet test tests/FileTidy.Tests --filter "FullyQualifiedName~Sync_AddsNew|FullyQualifiedName~EditDebounce"` — Expected: 编译失败（Sync 不存在）

- [ ] **Step 4: FolderWatcher 增量同步实现**

```csharp
// src/FileTidy.Core/FolderWatcher.cs
private void AddWatcher(string folder)
{
    var watcher = new FileSystemWatcher(folder)
    {
        IncludeSubdirectories = true,
        InternalBufferSize = 64 * 1024
    };
    watcher.Created += OnChanged;
    watcher.Renamed += OnChanged;
    watcher.Changed += OnChanged;
    watcher.Error += OnError;
    watcher.EnableRaisingEvents = true;
    _watchers.Add(watcher);
}

public void Watch(IReadOnlyList<string> folders)
{
    var targets = folders.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    lock (_lock)
    {
        if (_disposed) return;
        foreach (var folder in targets)
        {
            if (_watchers.Any(w => string.Equals(w.Path.TrimEnd('\\'), folder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
                continue;
            AddWatcher(folder);
        }
    }
}

/// <summary>增量同步监听：新增目录建监听、已移除目录停监听，现存 watcher 不动（区别于 Replace 的全量重建）</summary>
public void Sync(IReadOnlyList<string> folders)
{
    var targets = folders.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    lock (_lock)
    {
        if (_disposed) return;
        foreach (var folder in targets)
        {
            if (_watchers.Any(w => string.Equals(w.Path.TrimEnd('\\'), folder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
                continue;
            AddWatcher(folder);
        }
        foreach (var w in _watchers.ToList())
        {
            if (!targets.Any(t => string.Equals(t.TrimEnd('\\'), w.Path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
            {
                w.Dispose();
                _watchers.Remove(w);
            }
        }
    }
}
```

- [ ] **Step 5: MainViewModel 保存拆分**

```csharp
// MainViewModel 内新增字段与常量
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
    ApplyAndSave();
    SyncWatchers();
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
```

调用点替换（原 `Save()` 方法删除）：
- `Attach` 的 PropertyChanged → `DebouncedSave()`
- `DeleteRule`、`MoveRule`、`AutoTidy` setter、`AutoRenameOnConflict` setter、`StartWithWindows` setter、`LoadConfig` 内的隐式保存 → `SaveNow()`
- `AddRule` **不调用任何保存**（保持原行为：新规则靠首次编辑触发保存）
- `Shutdown()` 内的 `Save()` → `SaveNow()`（冲刷 pending 防抖）

- [ ] **Step 6: 测试通过**

Run: `dotnet test tests/FileTidy.Tests` — Expected: 103 PASS（新增 2 个）

- [ ] **Step 7: 提交**

```bash
git add src/FileTidy.Core/FolderWatcher.cs src/FileTidy.App/ViewModels/MainViewModel.cs tests/FileTidy.Tests/FolderWatcherTests.cs tests/FileTidy.Tests/MainViewModelTests.cs
git commit -m "perf: 规则编辑防抖落盘并增量同步监听，消除击键写盘与全量重建"
```

---

### Task 6: 自动整理忙时重试 + 延迟具名常量

**Files:**
- Modify: `src/FileTidy.App/ViewModels/MainViewModel.cs`（AutoTidyAsync）

**理由：** 忙时事件被静默丢弃（现 `catch (InvalidOperationException) { }`）会造成文件漏整理；改为 3 次退避重试，仍忙才丢弃。`Task.Delay(3000)` 提为具名常量。重试循环不写自动化测试：忙时拒绝语义已被 EngageQueueTests 覆盖，循环本身 8 行简单逻辑，等待时序难稳定（避免 flaky）。

- [ ] **Step 1: 改造 AutoTidyAsync**

```csharp
/// <summary>自动整理触发后的延迟：等待写文件完成，避免读到半成品</summary>
private const int AutoTidyDebounceMs = 3000;
/// <summary>队列忙时重试间隔与上限</summary>
private const int AutoTidyRetryIntervalMs = 500;
private const int AutoTidyMaxRetries = 3;

private async Task AutoTidyAsync()
{
    if (!AutoTidy) return;
    await Task.Delay(AutoTidyDebounceMs);
    for (var attempt = 1; attempt <= AutoTidyMaxRetries; attempt++)
    {
        try
        {
            await _queue.RunAsync(async () =>
            {
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
            return;
        }
        catch (InvalidOperationException)
        {
            if (attempt == AutoTidyMaxRetries) return; // 仍忙则丢弃本次触发，避免无限重试
            await Task.Delay(AutoTidyRetryIntervalMs);
        }
        catch (Exception ex)
        {
            TidyCompleted?.Invoke($"自动整理失败：{ex.Message}");
            return;
        }
    }
}
```

> 原结构外层 try/catch（InvalidOperationException 空吞 + Exception 通知）与队列任务内代码合并进循环，语义不变：异常类型优先级保持（InvalidOperationException 先于 Exception 匹配）。

- [ ] **Step 2: 全量测试**

Run: `dotnet test tests/FileTidy.Tests` — Expected: 103 PASS（AutoTidy_WatchesSource_DropsNewFile_MovesIt 端到端仍通过）

- [ ] **Step 3: 提交**

```bash
git add src/FileTidy.App/ViewModels/MainViewModel.cs
git commit -m "perf: 自动整理队列忙时退避重试，减少事件丢弃；延迟与重试参数提为具名常量"
```

---

### Task 7: watcher 目录消失时的监听自清理

**Files:**
- Modify: `src/FileTidy.Core/FolderWatcher.cs`（OnError 增强）

**理由与边界：** 源目录被删后 FileSystemWatcher 无法恢复监听（目录不存在无法创建 watcher），但旧 watcher 对象残留。OnError 时若目录已不存在则释放该 watcher；目录重建后由下一次 Sync/Replace（编辑、开关切换、重启）重新监听。不做后台轮询（范围外）。`catch { }` 保留但内部改为先移除已消失目录。

- [ ] **Step 1: 改造 OnError**

```csharp
private void OnError(object sender, ErrorEventArgs e)
{
    // 缓冲溢出或目录被删：对已不存在的目录释放 watcher（重建后由 Sync/Replace 重新监听），
    // 剩余 watcher 复位事件开关并触发一次重扫，避免溢出期间的文件变更永久丢失
    lock (_lock)
    {
        foreach (var w in _watchers.ToList())
        {
            if (Directory.Exists(w.Path)) continue;
            w.Dispose();
            _watchers.Remove(w);
        }
        foreach (var w in _watchers)
        {
            try { w.EnableRaisingEvents = false; w.EnableRaisingEvents = true; }
            catch { }
        }
    }
    if (_watchers.Count > 0) TidyTriggered?.Invoke();
}
```

> 无自动化测试：FileSystemWatcher 的 Error 事件无法确定性触发（既有 2 个已知不可测盲区之一，同 spec/2026-08-06-supplement-tests-design.md §5）。

- [ ] **Step 2: 构建 + 既有测试**

Run: `dotnet build --nologo -v q; dotnet test tests/FileTidy.Tests` — Expected: 0 错误，103 PASS

- [ ] **Step 3: 提交**

```bash
git add src/FileTidy.Core/FolderWatcher.cs
git commit -m "fix: 监听目录被删时释放对应 watcher，避免残留对象与监听失效"
```

---

### Task 8: 操作日志 JSON 统一 camelCase（旧日志兼容）

**Files:**
- Modify: `src/FileTidy.Core/OperationLog.cs`
- Test: `tests/FileTidy.Tests/OperationLogTests.cs`（补旧格式兼容用例）

**兼容设计：** 写用 camelCase；读用 `PropertyNameCaseInsensitive = true`（大小写不敏感），新旧格式都能读。

- [ ] **Step 1: 写失败测试**

```csharp
// tests/FileTidy.Tests/OperationLogTests.cs 追加（复用类内 _dir 临时目录，见既有用例风格）
[Fact]
public void Latest_ReadsLegacyPascalCaseLog()
{
    // 旧版本序列化无选项，属性名为 PascalCase（Timestamp/Entries/Source/Dest）
    File.WriteAllText(Path.Combine(_dir, "op-20260101000000000.json"),
        "{\"Timestamp\":\"2026-01-01T00:00:00\",\"Entries\":[{\"Source\":\"C:\\\\a.txt\",\"Dest\":\"C:\\\\b.txt\"}]}");

    var log = new OperationLog(_dir, 10);
    var latest = log.Latest();

    Assert.NotNull(latest);
    Assert.Single(latest!.Entries);
    Assert.Equal("C:\\b.txt", latest.Entries[0].Dest);
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/FileTidy.Tests --filter FullyQualifiedName~ReadsLegacyPascalCase` — Expected: FAIL（属性名不匹配，Entries 为空）

> **实测更正（2026-08-11）**：该测试对本变更**不可红**——CLR 属性名（Timestamp/Entries/Source/Dest）本就为 PascalCase，System.Text.Json 默认无命名策略时与旧日志属性完全精确匹配，实测实现前即 PASS。其价值在于：①若实现时漏掉 `PropertyNameCaseInsensitive = true`，Web 默认 CamelCase 策略会将旧日志键归一为 `timestamp` 后与 CLR `Timestamp` 做大小写敏感比较而失配（Entries 为空），测试即红；②与写侧断言用例（Step 1 追加的 `Save_WritesCamelCaseJson`）共同构成写读双向格式守约。写侧断言用例：Save 后 `File.ReadAllText` 断言含 `"timestamp"`/`"dest"` 且不含 `"Timestamp"`（实测：临时回退 Save 为默认 options 时该测试变红，恢复后 106 PASS）。

- [ ] **Step 3: 实现**

```csharp
// src/FileTidy.Core/OperationLog.cs 顶部
private static readonly JsonSerializerOptions LogOptions = new(JsonSerializerDefaults.Web)
{
    PropertyNameCaseInsensitive = true // 兼容旧版 PascalCase 日志
};

// Save 改为：
File.WriteAllText(path, JsonSerializer.Serialize(record, LogOptions));

// Latest 改为：
return JsonSerializer.Deserialize<OperationRecord>(File.ReadAllText(file.FullName), LogOptions);
```

- [ ] **Step 4: 测试通过 + 全量**

Run: `dotnet test tests/FileTidy.Tests` — Expected: 104 PASS（含既有"跨重启持久化日志撤销"用例）

- [ ] **Step 5: 提交**

```bash
git add src/FileTidy.Core/OperationLog.cs tests/FileTidy.Tests/OperationLogTests.cs
git commit -m "refactor: 操作日志 JSON 统一 camelCase 并兼容读取旧版 PascalCase 日志"
```

---

### Task 9: 预览/整理/撤销样板抽取

**Files:**
- Modify: `src/FileTidy.App/ViewModels/MainViewModel.cs`

**设计：** 三个命令共有的骨架——Busy 置位、互斥队列、后台执行、状态与明细更新、忙/异常/最终处理——抽为 `RunExclusiveAsync`。各命令只保留自身执行逻辑与文案。

- [ ] **Step 1: 先全量测试建立基线**

Run: `dotnet test tests/FileTidy.Tests` — Expected: 104 PASS

- [ ] **Step 2: 抽取公共执行骨架**

> **关键：不得用 `Task.Run(execute)` 整体包装 execute**——execute 内需要"后台算 + UI 更新"交错（如 Preview 的 RenderPreviews 必须在 UI 线程，测试环境无 SynchronizationContext 时延续在池线程无碍）。因此骨架内直接 `await execute()`，execute 自身用 `await Task.Run(...)` 控制后台段，`_queue.RunAsync` 委托在调用上下文（UI）执行。

```csharp
/// <summary>一次后台操作的执行结果：Status 为空则不改状态文案；Failed/Skipped 为 null 则不改明细</summary>
private sealed record RunOutcome(string? Status, IReadOnlyList<OrganizeItem>? Failed, IReadOnlyList<OrganizeItem>? Skipped);

/// <summary>命令执行骨架：互斥队列 + 状态/明细更新 + 忙拒绝与异常文案；execute 内部自行划分后台 IO 与 UI 更新</summary>
private async Task RunExclusiveAsync(string busyText, string busyRejectText, string errorText,
    Func<Task<RunOutcome>> execute)
{
    if (Busy) return;
    try
    {
        Busy = true;
        StatusText = busyText;
        await _queue.RunAsync(async () =>
        {
            var outcome = await execute();
            if (outcome.Status is not null) StatusText = outcome.Status;
            if (outcome.Failed is not null || outcome.Skipped is not null)
                SetErrorDetails(outcome.Failed ?? Array.Empty<OrganizeItem>(), outcome.Skipped ?? Array.Empty<OrganizeItem>());
            return true;
        });
    }
    catch (InvalidOperationException) { StatusText = busyRejectText; }
    catch (Exception ex)
    {
        StatusText = errorText;
        SetErrorDetails(new[] { new OrganizeItem { Source = "", Reason = ex.Message } }, Array.Empty<OrganizeItem>());
    }
    finally { Busy = false; }
}
```

- [ ] **Step 3: 三个命令改写**

```csharp
private async Task PreviewAsync()
    => await RunExclusiveAsync("正在扫描…", "整理正在进行中，请稍候", "预览失败", async () =>
    {
        var previews = await Task.Run(() => BuildPreview(render: false)); // 扫描后台，渲染回 UI
        RenderPreviews(previews);
        return new RunOutcome($"预览完成，共 {previews.Count} 个文件", Array.Empty<OrganizeItem>(), Array.Empty<OrganizeItem>());
    });

private async Task TidyAsync()
    => await RunExclusiveAsync("正在整理…", "整理正在进行中，请稍候", "整理失败", async () =>
    {
        var (result, earlyExit) = await Task.Run(() =>
        {
            var previews = BuildPreview(render: false);
            // 传完整批次给 Organizer：TemplateError 计失败、Moved 执行移动，其余（未命中/冲突）自动忽略
            var movable = previews.Where(p => p.Status == PreviewStatus.Moved).ToList();
            var templateErrors = previews.Where(p => p.Status == PreviewStatus.TemplateError).ToList();
            if (movable.Count == 0 && templateErrors.Count == 0) return (Result: (OrganizeResult?)null, EarlyExit: true);
            var (result, record) = Organizer.Execute(previews, _now());
            if (record.Entries.Count > 0) new OperationLog(_operationsDir, _retention).Save(record);
            return (Result: result, EarlyExit: false);
        });
        if (earlyExit) return new RunOutcome("没有需要整理的文件", null, null);
        return new RunOutcome(
            $"整理完成：成功 {result!.Succeeded}，跳过 {result.Skipped.Count}，失败 {result.Failed.Count}",
            result.Failed, result.Skipped);
    });

private async Task UndoAsync()
    => await RunExclusiveAsync("正在撤销…", "整理正在进行中，请稍候", "撤销失败", async () =>
    {
        var (result, found) = await Task.Run(() =>
        {
            var log = new OperationLog(_operationsDir, _retention);
            var record = log.Latest();
            if (record is null) return (Result: (Organizer.UndoResult?)null, Found: false);
            var result = Organizer.Undo(record);
            log.DiscardLatest();
            return (Result: result, Found: true);
        });
        if (!found) return new RunOutcome("没有可撤销的操作", null, null);
        return new RunOutcome($"已撤销：还原 {result!.Restored}，跳过 {result.Skipped.Count}",
            Array.Empty<OrganizeItem>(), result.Skipped);
    });
```

> 注：`earlyExit`/`found` 是独立布尔，编译器不关联元组可空性，访问前用 `result!`/`result!.` 显式非空断言。

- [ ] **Step 4: 全量测试**

Run: `dotnet test tests/FileTidy.Tests` — Expected: 104 PASS（16 个 MainViewModel 用例全覆盖三命令文案）

- [ ] **Step 5: 提交**

```bash
git add src/FileTidy.App/ViewModels/MainViewModel.cs
git commit -m "refactor: 抽取预览/整理/撤销共用执行骨架，消除三份重复样板"
```

---

### Task 10: 剩余具名常量

**Files:**
- Modify: `src/FileTidy.App/MainWindow.xaml.cs`（拖拽阈值）
- Modify: `src/FileTidy.Core/FolderWatcher.cs`（缓冲大小）
- Modify: `src/FileTidy.Core/RegexCondition.cs`（若 500ms 超时为字面量则提为常量）

- [ ] **Step 1: 检查 RegexCondition.cs:15 现状**

```powershell
Select-String -LiteralPath "src\FileTidy.Core\RegexCondition.cs" -Pattern "500"
```

若已是 `private const int MatchTimeoutMs = 500;` 则跳过该项；若是 `new Regex(..., TimeSpan.FromMilliseconds(500))` 字面量则提为常量。

- [ ] **Step 2: 具名常量**

```csharp
// MainWindow.xaml.cs
/// <summary>拖拽判定阈值（像素）：小于该距离视为点击而非拖拽</summary>
private const double DragThreshold = 10;
// 使用处：if (Math.Abs(pos.X - _dragStart.X) < DragThreshold && Math.Abs(pos.Y - _dragStart.Y) < DragThreshold) return;

// FolderWatcher.cs
private const int InternalBufferSize = 64 * 1024;
// 使用处：InternalBufferSize = InternalBufferSize 冲突——属性名与常量同名时改属性赋值来源：
// 定义 static readonly 或改名常量 WatchBufferSizeBytes = 64 * 1024
```

> FolderWatcher 属性 `InternalBufferSize` 与常量同名冲突：常量命名为 `WatchBufferSizeBytes`。

- [ ] **Step 3: 构建 + 全量测试**

Run: `dotnet build --nologo -v q; dotnet test tests/FileTidy.Tests` — Expected: 0 错误，104 PASS

- [ ] **Step 4: 提交**

```bash
git add src/FileTidy.App/MainWindow.xaml.cs src/FileTidy.Core/FolderWatcher.cs src/FileTidy.Core/RegexCondition.cs
git commit -m "refactor: 拖拽阈值/监听缓冲等魔法数字提为具名常量"
```

---

## Self-Review 检查

- **覆盖核对**：#1→Task 4、#2→Task 2、#3→Task 5、#4→Task 1、#5→Task 3、#6→Task 6、#7→Task 7、#8→Task 8、#9→Task 9、#10→Task 10，全部 10 项有任务。
- **类型一致性**：`SingleInstanceGuard.IsFirstInstance`、`FolderWatcher.Sync`、`RunOutcome`、`SaveNow/DebouncedSave/ApplyAndSave/SyncWatchers`、`WatchBufferSizeBytes` 均在定义任务中明确签名，无跨任务引用漂移；Task 4 与 Task 9 的元组均已统一命名元组（`Handled`/`Found` 分支），无隐式类型不一致。
- **测试影响面**：Task 4/9 改三命令但文案字符串保持不变（既有 16 个 VM 用例断言文案）；Task 5 的 MoveRule/开关切换走 `SaveNow` 保持即时性（MoveRule_ReordersAndPersists、AutoTidy 端到端测试依赖）；AddRule 保持不保存的原行为；Task 6 重试不引入时序测试。
- **修复记录（初稿审查发现的 9 处问题）**：
  1. Task 1 原含提交步骤——`*.pem` 未跟踪，删除无 git 变更，已删除提交步骤。
  2. Task 4 原 TidyAsync 两分支元组类型不一致（`(string,[] ,[],bool)` vs `(OrganizeResult,OperationRecord,bool)`）无法编译——已统一为 `(OrganizeResult?, List<OrganizeItem>?, List<OrganizeItem>?, bool)`。
  3. Task 4 UndoAsync 同上（`(null,null,bool)` vs `(UndoResult,OperationRecord,bool)`）——已统一 `(UndoResult?, bool)`。
  4. Task 4 PreviewAsync 残留 `previews is not null` 冗余判断——已删除。
  5. Task 5 Sync 测试 `Sync` 后创建目录 c——Sync 只监听已存在目录，断言必失败——已改为先建目录再 Sync；辅助目录改从现有 `_dir`/`dir2` 风格（无 TempDir 类）；抛异常 handler 改计数器断言（异常会传播到 watcher 线程导致进程崩溃）。
  6. Task 5 EditDebounce 测试残留未使用变量 `writes1`——已删除，并增加"防抖窗口内未落盘"断言。
  7. Task 5 AddRule 被错误列入"立即保存"集合——原行为不保存，已注明排除。
  8. Task 8 测试 `using var dir = Directory.CreateTempSubdirectory()`——DirectoryInfo 非 IDisposable 不编译——已改类内 `_dir` 字段风格。
  9. Task 9 骨架 `Task.Run(execute)` 会把 RenderPreviews 丢进池线程（WPF 集合绑定要求 UI 线程）——已改 `await execute()`，execute 内部自行划分后台段与 UI 更新。
  - 附：Task 3 增补 System.Drawing 可用性风险预案（编译失败回退方案）。
