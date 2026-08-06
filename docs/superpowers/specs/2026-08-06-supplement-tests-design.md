# 测试用例补充设计文档

- 日期：2026-08-06
- 状态：已确认
- 范围：仅新增测试用例，不修改任何生产代码
- 目标：补齐审查发现的覆盖缺口（3 个已知缺口 + 5 个未覆盖分支），用例总数 106 → 114

## 1. 背景

首轮测试执行（106 个用例全部通过）后的用例审查发现：

- 已明确的覆盖缺口（3 个）：
  1. `Organizer.Execute` 无执行期冲突改名用例（仅 Preview 有冲突用例）
  2. `PathHelper` 无扩展名缺失文件的序号测试
  3. `EngageQueue` 无并发入队测试
- 阅读实现代码时新发现的未覆盖分支（5 个）：
  4. `PreviewStatus.Conflict`：`AutoRenameOnConflict=false` 且目标已存在（PreviewService.cs `ResolveDest`）
  5. `Organizer.Execute` 执行期"不再匹配规则"跳过分支（Organizer.cs:51-55 再校验）
  6. `Organizer.Undo` 部分失败不中断其余条目（Undo 的 `foreach continue` 语义）
  7. `FolderWatcher.Replace` 后旧目录不再触发
  8. `PathHelper.GetUniquePath` 目标被目录占用（`Directory.Exists` 分支）

## 2. 方案

在现有 6 个测试类内直接追加 `[Fact]` 用例，沿用项目既有模式：

- 真实临时目录（`Directory.CreateTempSubdirectory`）操作真实文件
- 全 `[Fact]` 裸用例，不使用 `[Theory]`（与现有风格一致）
- 中文注释、中文断言消息
- 复用各测试类现有 helper（`Moved`、`Write`、`MakeFile` 等）

理由：缺口全部落在已有测试类的职责边界内（PathHelperTests / PreviewServiceTests / OrganizerTests / OrganizerUndoTests / EngageQueueTests / FolderWatcherTests），原地追加最符合项目惯例，避免测试类数量膨胀。

## 3. 新增用例明细

### 3.1 PathHelperTests（+2）

| 用例 | 验证点 |
|---|---|
| `GetUniquePath_NoExtension_AppendsNumber` | 无扩展名文件 `报告` 已存在 → 返回 `报告(1)`（验证扩展名为空时的拼接正确） |
| `GetUniquePath_DirectoryOccupied_AppendsNumber` | 目标路径被**目录**占用 → 返回 `目标(1)`（覆盖 `Directory.Exists` 检查分支） |

### 3.2 PreviewServiceTests（+1）

| 用例 | 验证点 |
|---|---|
| `Build_ConflictStatus_WhenNoAutoRename` | `AutoRenameOnConflict=false` 且目标文件已存在 → `Status == Conflict`、`DestPath` 保持原冲突路径（不追加序号） |

前置：真实目录中创建目标文件；规则 `SourcePath = _dir`、`TargetPath = 目标目录`、`AutoRenameOnConflict = false`、扩展名条件命中源文件。

### 3.3 OrganizerTests（+2）

| 用例 | 验证点 |
|---|---|
| `Execute_AutoRenameOnConflict_WhenDestTakenAtRunTime` | 预览时目标空闲；执行前该目标被占用 → Execute 执行期通过 `GetUniquePath` 二次唯一化，文件落到 `a(1).txt`，`Succeeded=1`，操作记录 `Dest` 为唯一化后的路径 |
| `Execute_NoLongerMatchesRule_GoesToSkipped` | `MatchedRule` 带 `AgeCondition Days=3`，文件 `LastWriteTime = now-5天` → Execute 再校验失败 → `Skipped` 且 Reason 含 `"不再匹配规则"`，文件留在原地 |

前置（冲突改名）：真实目录，`MatchedRule.AutoRenameOnConflict = true`，构造 `PreviewEntry(Status=Moved)`，执行前在 `DestPath` 写入同名文件。

### 3.4 OrganizerUndoTests（+1）

| 用例 | 验证点 |
|---|---|
| `Undo_PartialFailure_ContinuesOthers` | 记录含 2 条：1 条目标缺失（Skipped）、1 条正常（Restored）→ `Restored=1`、`Skipped=1`、正常条目仍被移回源 |

### 3.5 EngageQueueTests（+1）

| 用例 | 验证点 |
|---|---|
| `RunAsync_Concurrent_OnlyOneSucceeds` | 8 个并发 `Task.Run` 同时调用 `RunAsync`；首个任务的 work 内 `await` 门控 `TaskCompletionSource` 保持忙窗口；释放门后断言恰 1 个成功、7 个抛 `InvalidOperationException`；此后队列空闲，可再次 `RunAsync` 成功 |

设计要点（确定性优先，避免 flaky）：门控 TCS 保证忙窗口足够长；每个任务在尝试 `RunAsync` 后以 `Interlocked` 递增尝试计数，主线程轮询等待尝试计数达到 8 后再释放门——确保 8 个任务全部完成"进入/被拒"流程，杜绝时序竞态。

### 3.6 FolderWatcherTests（+1）

| 用例 | 验证点 |
|---|---|
| `Replace_OldFolderNoLongerTriggers` | `Watch([dir1]) → Replace([dir2])` 后向 dir1 写文件，等待 500ms 断言触发计数不增加；再向 dir2 写文件确认 watcher 仍存活（防误杀） |

## 4. 验收标准

1. `dotnet test tests/FileTidy.Tests` 全部通过，总数 114
2. 全部用例运行稳定（重复执行 3 次无 flaky）
3. 不修改任何 `src/` 生产代码
4. 注释与断言消息为简体中文

## 5. 已知剩余缺口（无法确定性测试，记录在案）

以下两项为需求文档承诺的行为，但**无法以确定性测试覆盖**，本期明确排除，不影响验收：

| 需求点 | 生产位置 | 无法测原因 |
|---|---|---|
| 跨卷 Copy+Delete 中途失败时删除已复制副本（回滚） | Organizer.cs `Move` 的 catch 分支（`File.Delete(dest)`） | 需模拟 `File.Copy` 中途失败（磁盘写满 / 目标只读等），无法在临时目录中可靠、可复现地构造 |
| FileSystemWatcher 缓冲溢出自动重建监听并重扫 | FolderWatcher.cs `OnError` 分支 | 需持续快速写入以撑爆 64KB 内部缓冲区，触发时刻与次数不可控，属天然 flaky 场景 |

以上两项依赖系统级故障注入或时间竞争，保持人工验收作为兜底（对应一期需求文档 §5 错误处理表）。
