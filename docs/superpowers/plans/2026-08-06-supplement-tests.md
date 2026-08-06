# 测试用例补充实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 6 个既有测试类中追加 8 个测试用例，覆盖审查发现的缺口，用例总数 106 → 114。

**Architecture:** 全部为纯测试新增，不修改 `src/` 生产代码。每个任务对应一个测试类，沿用各测试类现有 helper（`Moved`、`Write` 等）与真实临时目录模式，全部 `[Fact]` 用例。

**Tech Stack:** C# / .NET 8 / xUnit（`tests/FileTidy.Tests`，net8.0-windows）

## Global Constraints

- 不修改任何 `src/FileTidy.Core`、`src/FileTidy.App` 生产代码
- 测试用真实临时目录（`Directory.CreateTempSubdirectory`）操作真实文件
- 全 `[Fact]` 裸用例，不用 `[Theory]`，不用 mock
- 代码注释与断言消息一律简体中文
- 测试命令：`& "C:\Program Files\dotnet\dotnet.exe" test tests/FileTidy.Tests`（`dotnet` 不在 PATH）
- 提交信息用 conventional 风格（`test:`）简体中文

---

### Task 1: PathHelperTests 补 2 个用例

**Files:**
- Modify: `tests/FileTidy.Tests/PathHelperTests.cs`（类尾 `GetUniquePath_ReturnsOriginalWhenFree` 之后追加）

**Interfaces:**
- Consumes: 现有 `PathHelper.GetUniquePath(string) → string`、测试类字段 `_dir`
- Produces: 2 个新用例（无其他任务依赖）

- [ ] **Step 1: 追加用例代码**

在 `PathHelperTests` 类内追加：

```csharp
    [Fact]
    public void GetUniquePath_NoExtension_AppendsNumber()
    {
        // 无扩展名文件：扩展名为空时序号拼接不得出错
        var p = Path.Combine(_dir, "报告");
        File.WriteAllText(p, "x");

        Assert.Equal(Path.Combine(_dir, "报告(1)"), PathHelper.GetUniquePath(p));
    }

    [Fact]
    public void GetUniquePath_DirectoryOccupied_AppendsNumber()
    {
        // 目标路径被目录占用时同样追加序号（Directory.Exists 分支）
        var p = Path.Combine(_dir, "目录");
        Directory.CreateDirectory(p);

        Assert.Equal(Path.Combine(_dir, "目录(1)"), PathHelper.GetUniquePath(p));
    }
```

- [ ] **Step 2: 运行测试验证通过**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test tests/FileTidy.Tests --filter "FullyQualifiedName~PathHelperTests"`
Expected: 4 通过、0 失败

- [ ] **Step 3: 提交**

```bash
git add tests/FileTidy.Tests/PathHelperTests.cs
git commit -m "test: 补充 PathHelper 无扩展名与目录占用序号用例"
```

### Task 2: PreviewServiceTests 补 1 个用例

**Files:**
- Modify: `tests/FileTidy.Tests/PreviewServiceTests.cs`（`Build_AutoRenamesOnDestConflict` 之后追加）

**Interfaces:**
- Consumes: 现有 `PreviewService.Build(List<Rule>, FileEntry[], DateTime)`、`PreviewStatus`、`Rule.AutoRenameOnConflict`（默认 true，需显式 false）、测试类字段 `_dir`
- Produces: 1 个新用例（无其他任务依赖）

- [ ] **Step 1: 追加用例代码**

在 `PreviewServiceTests` 类内追加：

```csharp
    [Fact]
    public void Build_ConflictStatus_WhenNoAutoRename()
    {
        // 未启用自动序号时目标已存在 → Conflict 状态，DestPath 保持原冲突路径
        var target = Path.Combine(_dir, "target");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "a.jpg"), "existing");

        var rules = new List<Rule>
        {
            new() { Name = "图", SourcePath = _dir, TargetPath = target, AutoRenameOnConflict = false,
                    Conditions = { new ExtensionCondition { Extensions = { "jpg" } } } }
        };
        var srcFile = Path.Combine(_dir, "a.jpg");
        File.WriteAllText(srcFile, "new");

        var file = new FileEntry { FullPath = srcFile, FileName = "a.jpg", Extension = "jpg", LastWriteTime = DateTime.Now };
        var previews = PreviewService.Build(rules, new[] { file }, DateTime.Now);

        Assert.Equal(PreviewStatus.Conflict, previews[0].Status);
        Assert.Equal(Path.Combine(target, "a.jpg"), previews[0].DestPath);
    }
```

- [ ] **Step 2: 运行测试验证通过**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test tests/FileTidy.Tests --filter "FullyQualifiedName~PreviewServiceTests"`
Expected: 11 通过、0 失败

- [ ] **Step 3: 提交**

```bash
git add tests/FileTidy.Tests/PreviewServiceTests.cs
git commit -m "test: 补充未启用自动序号时 Conflict 状态用例"
```

### Task 3: OrganizerTests 补 2 个用例

**Files:**
- Modify: `tests/FileTidy.Tests/OrganizerTests.cs`（类尾 `Execute_CrossVolumeUsesCopyDelete` 之后追加）

**Interfaces:**
- Consumes: 现有 `Moved(string,string)` helper（构造 `PreviewEntry`，`MatchedRule.AutoRenameOnConflict` 为默认 true）、`Organizer.Execute(IReadOnlyList<PreviewEntry>, DateTime)`、`OrganizeResult`、`OperationRecord`、`AgeCondition`
- Produces: 2 个新用例（无其他任务依赖）

- [ ] **Step 1: 追加用例代码**

在 `OrganizerTests` 类内追加：

```csharp
    [Fact]
    public void Execute_AutoRenameOnConflict_WhenDestTakenAtRunTime()
    {
        // 预览时目标空闲；执行前目标被占用 → 执行期二次唯一化落到 a(1).txt
        var src = Write("a.txt");
        var dest = Path.Combine(_root, "out", "a.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.WriteAllText(dest, "occupied");

        var (result, record) = Organizer.Execute(new List<PreviewEntry> { Moved(src, dest) }, DateTime.Now);

        Assert.Equal(1, result.Succeeded);
        Assert.True(File.Exists(Path.Combine(_root, "out", "a(1).txt")));
        Assert.False(File.Exists(src));
        Assert.Equal(Path.Combine(_root, "out", "a(1).txt"), record.Entries[0].Dest);
    }

    [Fact]
    public void Execute_NoLongerMatchesRule_GoesToSkipped()
    {
        // 预览于 6 天前进行（文件年龄 11 天 ≥ 10 命中）；执行时年龄 5 天 < 10 → 再校验失败
        var src = Write("old.txt");
        var now = new DateTime(2026, 8, 6, 12, 0, 0);
        var entry = new PreviewEntry
        {
            File = new FileEntry { FullPath = src, FileName = "old.txt", Extension = "txt", LastWriteTime = now.AddDays(-5) },
            MatchedRule = new Rule
            {
                TargetPath = Path.Combine(_root, "out"),
                Conditions = { new AgeCondition { Days = 10 } }
            },
            DestPath = Path.Combine(_root, "out", "old.txt"),
            Status = PreviewStatus.Moved
        };

        var (result, _) = Organizer.Execute(new List<PreviewEntry> { entry }, now);

        Assert.Equal(0, result.Succeeded);
        Assert.Single(result.Skipped);
        Assert.Equal("不再匹配规则", result.Skipped[0].Reason);
        Assert.True(File.Exists(src));
    }
```

- [ ] **Step 2: 运行测试验证通过**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test tests/FileTidy.Tests --filter "FullyQualifiedName~OrganizerTests"`
Expected: 5 通过、0 失败（含跨卷用例；若本机无空闲盘符该用例会失败，属环境限制，与本次改动无关）

- [ ] **Step 3: 提交**

```bash
git add tests/FileTidy.Tests/OrganizerTests.cs
git commit -m "test: 补充执行期冲突改名与不再匹配规则跳过用例"
```

### Task 4: OrganizerUndoTests 补 1 个用例

**Files:**
- Modify: `tests/FileTidy.Tests/OrganizerUndoTests.cs`（类尾 `Undo_AcrossRestart_UsesPersistedLog` 之后追加）

**Interfaces:**
- Consumes: 现有 `Organizer.Undo(OperationRecord)`、`OperationRecord.Entries`、`LogEntry`、测试类字段 `_root`
- Produces: 1 个新用例（无其他任务依赖）

- [ ] **Step 1: 追加用例代码**

在 `OrganizerUndoTests` 类内追加：

```csharp
    [Fact]
    public void Undo_PartialFailure_ContinuesOthers()
    {
        // 一条缺失（Skipped）不得中断其余条目恢复
        var src = Path.Combine(_root, "a.txt");
        var dest = Path.Combine(_root, "out", "a.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.WriteAllText(dest, "x");

        var record = new OperationRecord
        {
            Timestamp = DateTime.Now,
            Entries =
            {
                new LogEntry { Source = Path.Combine(_root, "gone.txt"), Dest = Path.Combine(_root, "out", "gone.txt") },
                new LogEntry { Source = src, Dest = dest }
            }
        };
        var result = Organizer.Undo(record);

        Assert.Equal(1, result.Restored);
        Assert.Single(result.Skipped);
        Assert.True(File.Exists(src));
        Assert.False(File.Exists(dest));
    }
```

- [ ] **Step 2: 运行测试验证通过**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test tests/FileTidy.Tests --filter "FullyQualifiedName~OrganizerUndoTests"`
Expected: 4 通过、0 失败

- [ ] **Step 3: 提交**

```bash
git add tests/FileTidy.Tests/OrganizerUndoTests.cs
git commit -m "test: 补充撤销部分失败不中断其余条目用例"
```

### Task 5: EngageQueueTests 补 1 个并发用例

**Files:**
- Modify: `tests/FileTidy.Tests/EngageQueueTests.cs`（类尾 `RunAsync_RejectsWhenBusy` 之后追加）
- 该文件需新增 `using System.Threading;` 以使用 `Interlocked`/`Volatile`

**Interfaces:**
- Consumes: 现有 `EngageQueue.RunAsync<T>(Func<Task<T>>)`、`InvalidOperationException`
- Produces: 1 个新用例（无其他任务依赖）

- [ ] **Step 1: 追加用例代码**

文件顶部追加 `using System.Threading;`，类内追加：

```csharp
    [Fact]
    public async Task RunAsync_Concurrent_OnlyOneSucceeds()
    {
        // 8 个并发调用恰 1 个成功；门控 TCS 保证忙窗口，轮询计数保证全部已尝试
        var q = new EngageQueue();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        var succeeded = 0;
        var rejected = 0;

        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            Interlocked.Increment(ref attempts);
            try
            {
                await q.RunAsync(async () => { await gate.Task; return 1; });
                Interlocked.Increment(ref succeeded);
            }
            catch (InvalidOperationException)
            {
                Interlocked.Increment(ref rejected);
            }
        })).ToArray();

        // 等待全部任务完成"进入或被拒"后再释放门，杜绝时序竞态
        while (Volatile.Read(ref attempts) < 8) await Task.Delay(10);
        gate.SetResult();
        await Task.WhenAll(tasks);

        Assert.Equal(1, succeeded);
        Assert.Equal(7, rejected);

        // 队列恢复空闲，可再次执行
        Assert.Equal(2, await q.RunAsync(async () => 2));
    }
```

- [ ] **Step 2: 运行测试验证通过**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test tests/FileTidy.Tests --filter "FullyQualifiedName~EngageQueueTests"`
Expected: 3 通过、0 失败

- [ ] **Step 3: 提交**

```bash
git add tests/FileTidy.Tests/EngageQueueTests.cs
git commit -m "test: 补充 EngageQueue 并发入队仅一成功用例"
```

### Task 6: FolderWatcherTests 补 1 个用例

**Files:**
- Modify: `tests/FileTidy.Tests/FolderWatcherTests.cs`（类尾 `Watch_IsIdempotentForSameFolder` 之后追加）

**Interfaces:**
- Consumes: 现有 `FolderWatcher.Watch(string[])`、`FolderWatcher.Replace(string[])`、`TidyTriggered` 事件、测试类字段 `_dir`
- Produces: 1 个新用例（无其他任务依赖）

- [ ] **Step 1: 追加用例代码**

在 `FolderWatcherTests` 类内追加：

```csharp
    [Fact]
    public async Task Replace_OldFolderNoLongerTriggers()
    {
        // Replace 后旧目录的监听必须失效；新目录仍触发（watcher 存活）
        var dir2 = Path.Combine(Path.GetTempPath(), "watch3-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir2);
        try
        {
            _watcher = new FolderWatcher();
            var count = 0;
            _watcher.TidyTriggered += () => count++;
            _watcher.Watch(new[] { _dir });
            _watcher.Replace(new[] { dir2 });

            File.WriteAllText(Path.Combine(_dir, "old.txt"), "x");
            await Task.Delay(500);
            var afterOld = count;

            File.WriteAllText(Path.Combine(dir2, "new.txt"), "x");
            await Task.Delay(500);

            Assert.Equal(0, afterOld);           // 旧目录已停止监听
            Assert.True(count > afterOld);       // 新目录仍触发
        }
        finally
        {
            Directory.Delete(dir2, true);
        }
    }
```

- [ ] **Step 2: 运行测试验证通过**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test tests/FileTidy.Tests --filter "FullyQualifiedName~FolderWatcherTests"`
Expected: 4 通过、0 失败

- [ ] **Step 3: 提交**

```bash
git add tests/FileTidy.Tests/FolderWatcherTests.cs
git commit -m "test: 补充 Replace 后旧目录监听失效用例"
```

### Task 7: 全量回归

**Files:** 无

**Interfaces:**
- Consumes: 全部已完成任务

- [ ] **Step 1: 运行全量测试**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test tests/FileTidy.Tests`
Expected: 114 通过、0 失败

- [ ] **Step 2: 稳定性复跑**

Run 同上命令两次（共 3 次全量）
Expected: 每次均 114 通过、0 失败（FolderWatcher/EngageQueue 用例无 flaky）

- [ ] **Step 3: 提交收尾（如有遗漏改动）**

```bash
git status
```
Expected: 工作区干净（无未提交改动）
