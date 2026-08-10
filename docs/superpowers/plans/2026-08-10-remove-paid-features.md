# 去除收费项（全部功能免费化）实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 从 FileTidy（WPF 文件整理助手）中彻底删除 Pro 授权体系（激活码/试用/功能门控），全部功能无条件免费。

**Architecture:** 按编译依赖顺序分批删除：先让 App 层与 ViewModel 测试不再引用授权 API（此时 Core 授权类型仍在，全仓可编译），再连根删除 Core 授权类型并同步删除其测试与 LicenseTool 工具，然后清理 XAML 主题资源，最后更新 README 并做残留检查。

**Tech Stack:** C# / .NET 8 / WPF / xUnit

## Global Constraints

- 代码注释、提交信息一律简体中文；提交信息用 conventional 风格（`feat:` / `chore:` / `docs:`）
- 不得用 PowerShell 5.1 的 `Set-Content`/`Out-File` 写含中文的文件；修改文件必须用编辑器工具
- `dotnet build`（根目录）与 `dotnet test tests/FileTidy.Tests` 每任务结束必须全绿
- 每个任务结束必须 `git commit`
- 不新增任何替代授权机制；不重构无关注逻辑（规则引擎、路径唯一化等）
- 文件删除用 `Remove-Item -Recurse -Force`（git 已跟踪文件直接 `git rm -r` 亦可，随后 `git add -u` 收录删除）

---

### Task 1: App 层去除授权使用（MainViewModel、App.xaml.cs、MainViewModelTests）

**Files:**
- Modify: `src/FileTidy.App/ViewModels/MainViewModel.cs`
- Modify: `src/FileTidy.App/ViewModels/RuleEditorViewModel.cs`（仅注释「(Pro)」标记清理）
- Modify: `src/FileTidy.App/App.xaml.cs:22-35`
- Modify: `tests/FileTidy.Tests/MainViewModelTests.cs`

**Interfaces:**
- Consumes: `MainViewModel` 现有构造与属性（本任务修改其签名）
- Produces: `MainViewModel(SettingsService settings, Func<DateTime>? coreTimeProvider = null, string? operationsDir = null)`（license 参数删除）——Task 2 删 Core 类型的前提；`BuildPreview` 内部调用 `PreviewService.Build(Rules.ToList(), files, now)`（不传 isAllowed）

- [ ] **Step 1: 改动 MainViewModel.cs**

本任务删除以下成员（文件共 439 行，参照 `src/FileTidy.App/ViewModels/MainViewModel.cs`）：

a. 第 24 行字段 `private readonly LicenseService _license;` 删除。

b. 第 54 行 `public RelayCommand ActivateCommand { get; }` 删除。

c. 第 65 行构造签名：
```csharp
public MainViewModel(SettingsService settings, Func<DateTime>? coreTimeProvider = null, string? operationsDir = null, LicenseService? license = null)
```
改为：
```csharp
public MainViewModel(SettingsService settings, Func<DateTime>? coreTimeProvider = null, string? operationsDir = null)
```

d. 第 70 行 `_license = license ?? new LicenseService(LicenseKeys.AppPublicKeyPem, AppPaths.LicenseFile, AppPaths.TrialFile);` 删除。

e. 第 80-87 行 `ActivateCommand = new RelayCommand(...)` 整块删除。

f. 第 112 行 `RefreshLicenseState();` 调用删除。

g. 第 164-176 行 `RefreshLicenseState()` 方法整体删除。

h. 第 201-218 行属性整体删除：`LicenseStateText`（含 `_licText` 字段）、`ActivationCode`（含 `_code`）、`ActivateResult`（含 `_activateResult`）、`ActivateResultIsError`（含 `_actErr`）、`LicenseState`。

i. 第 251-257 行 `SetErrorDetailsWithProHint` 方法整体删除；其唯一调用点（第 351 行）改为调 `SetErrorDetails(result.Failed, result.Skipped);`。

j. 第 292 行 `var previews = PreviewService.Build(Rules.ToList(), files, now, _license.IsAllowed);` 改为 `var previews = PreviewService.Build(Rules.ToList(), files, now);`。

k. 第 307-311 行 `RenderPreviews` 中状态文案三元链删除 NeedsPro 分支：
```csharp
StatusText = p.Status == PreviewStatus.Moved ? "将移动"
           : p.Status == PreviewStatus.Conflict ? "冲突"
           : p.Status == PreviewStatus.TemplateError ? "模板错误"
           : "未命中",
```

l. 第 339 行 `_license.RecordTidyUse();` 删除。

m. 第 341 行注释 `// 传完整批次给 Organizer：NeedsPro 计跳过、TemplateError 计失败、Moved 执行移动，其余（未命中/冲突）自动忽略` 改为 `// 传完整批次给 Organizer：TemplateError 计失败、Moved 执行移动，其余（未命中/冲突）自动忽略`。

n. 第 343 行 `var blocked = previews.Where(p => p.Status is PreviewStatus.NeedsPro or PreviewStatus.TemplateError).ToList();` 改为 `var blocked = previews.Where(p => p.Status == PreviewStatus.TemplateError).ToList();`。

o. 第 411 行 `_license.RecordTidyUse();` 删除；第 413 行注释 `// 传完整批次：NeedsPro/TemplateError 计入跳过/失败统计` 改为 `// 传完整批次：TemplateError 计入失败统计`。

p. `ViewModels/RuleEditorViewModel.cs` 纯注释清理（不改变行为）：
- 第 37 行 `/// <summary>正则条件文本（Pro）；非空即启用正则条件</summary>` → `/// <summary>正则条件文本；非空即启用正则条件</summary>`
- 第 45 行 `/// <summary>动作类型：move（仅移动） / moveRename（移动并重命名，Pro）</summary>` → `/// <summary>动作类型：move（仅移动） / moveRename（移动并重命名）</summary>`
- 第 49 行 `/// <summary>重命名模板（Pro，选中 moveRename 时生效）</summary>` → `/// <summary>重命名模板（选中 moveRename 时生效）</summary>`

- [ ] **Step 2: 改动 App.xaml.cs 第 26-28 行**

```csharp
_vm = new MainViewModel(
    new SettingsService(AppPaths.ConfigFile),
    license: new LicenseService(LicenseKeys.AppPublicKeyPem, AppPaths.LicenseFile, AppPaths.TrialFile));
```
改为：
```csharp
_vm = new MainViewModel(new SettingsService(AppPaths.ConfigFile));
```

- [ ] **Step 3: 适配 MainViewModelTests.cs**

a. 第 15-20 行删除注释与 `TempLicense` 辅助方法：
```csharp
    // 临时许可证：每个用例独立密钥对与文件，避免污染真实试用/激活文件
    private static LicenseService TempLicense(string dir)
    {
        var (_, pub) = LicenseCodec.CreateKeyPair();
        return new LicenseService(pub, Path.Combine(dir, "license.json"), Path.Combine(dir, "trial.json"));
    }
```
同时删除文件顶部第 6 行 `using FileTidy.Core;`（该文件其余代码不再引用 Core 命名空间——注意确认，若还有引用则保留）。

b. 第 101-105 行 `NewVm` 构造删除 `license: TempLicense(_dir),` 一行。

c. 第 164-166 行 `LoadConfig_PopulatesEditorList` 的构造删除 `license: TempLicense(_dir));` 行尾参数（改为 `operationsDir: opsDir);`）。

d. 第 263 行与第 270 行 `MoveRule_ReordersAndPersists` 两处构造删除 `license: TempLicense(dir)` 参数。

e. 删除以下整个用例（含其 `try/finally` 结构）：
- 第 198-227 行 `Tidy_RecordsTrialUseAndBlocksProWhenExhausted`
- 第 277-311 行 `Tidy_MovedAndProBlockedMixed_RecordsSkippedWithHint`
- 第 313-332 行 `Activate_SetsProText`
- 第 334-349 行 `Activate_BadCode_SetsErrorFlag`
- 第 351-368 行 `Activate_ValidCode_ClearsErrorIsError`
- 第 394-412 行 `LicenseState_StartsTrial_BecomesProAfterActivate`

f. 第 229-255 行 `Tidy_RenameSequenceMatchesPreview` 保留：删除第 240-242 行构造中的 `license` 相关代码，构造改为：
```csharp
var vm = new MainViewModel(new SettingsService(Path.Combine(dir, "config.json")));
```
（第 240-241 行 `var (_, pub) = LicenseCodec.CreateKeyPair();` 与 `var license = new LicenseService(...)` 一并删除）

- [ ] **Step 4: 构建与测试验证**

Run: `dotnet build` 与 `dotnet test tests/FileTidy.Tests`
Expected: 全部成功（Core 授权类型仍在，LicenseServiceTests/LicenseToolTests 未受影响）

- [ ] **Step 5: 提交**

```bash
git add -A
git commit -m "refactor: 去除 App 层 Pro 授权使用（命令/状态/试用计数）"
```

---

### Task 2: 删除 Core 授权体系、其测试与 LicenseTool 工具

**Files:**
- Delete: `src/FileTidy.Core/LicenseService.cs`、`src/FileTidy.Core/LicenseCodec.cs`、`src/FileTidy.Core/LicenseKeys.cs`、`src/FileTidy.Core/ProFeature.cs`
- Delete: `tests/FileTidy.Tests/LicenseServiceTests.cs`、`tests/FileTidy.Tests/LicenseToolTests.cs`
- Delete: `tools/`（整个目录：FileTidy.LicenseTool.csproj、Program.cs、private_key.pem 及 bin/obj/dist）
- Modify: `src/FileTidy.Core/AppPaths.cs:15-19`、`src/FileTidy.Core/PreviewService.cs`、`src/FileTidy.Core/Organizer.cs:34-38`
- Modify: `src/FileTidy.Core/Models/FileCondition.cs`、`src/FileTidy.Core/Models/RuleAction.cs`、`src/FileTidy.Core/Models/RegexCondition.cs`
- Modify: `tests/FileTidy.Tests/OrganizerGateTests.cs`、`tests/FileTidy.Tests/RegexConditionTests.cs`、`tests/FileTidy.Tests/RuleActionTests.cs`、`tests/FileTidy.Tests/PreviewServiceTests.cs`
- Modify: `FileTidy.sln`

**Interfaces:**
- Consumes: Task 1 已移除 App 与 ViewModel 测试对下列类型的全部引用
- Produces: Core 不再含任何授权概念；`PreviewService.Build(IReadOnlyList<Rule> rules, IEnumerable<FileEntry> files, DateTime now)`（4 参变 3 参）；`PreviewStatus` 无 NeedsPro

- [ ] **Step 1: 删除 Core 授权文件与工具目录、sln 条目**

```powershell
Remove-Item -Recurse -Force src/FileTidy.Core/LicenseService.cs, src/FileTidy.Core/LicenseCodec.cs, src/FileTidy.Core/LicenseKeys.cs, src/FileTidy.Core/ProFeature.cs
Remove-Item -Recurse -Force tests/FileTidy.Tests/LicenseServiceTests.cs, tests/FileTidy.Tests/LicenseToolTests.cs
Remove-Item -Recurse -Force tools
```

`FileTidy.sln` 删除：
- 第 16-19 行（tools 解文件夹 + LicenseTool 项目两行）
- 第 41-44 行（LicenseTool 的 Debug/Release 配置块）
- 第 50 行（`{A90985C0-...} = {578B2115-...}` 嵌套条目）

- [ ] **Step 2: 修改 AppPaths.cs**

删除第 15-19 行：
```csharp
    /// <summary>激活码文件路径</summary>
    public static string LicenseFile => Path.Combine(Root, "license.json");

    /// <summary>试用状态文件路径</summary>
    public static string TrialFile => Path.Combine(Root, "trial.json");
```

- [ ] **Step 3: 修改三个模型文件**

`FileCondition.cs` 第 16 行 `public virtual ProFeature? RequiredFeature => null;` 删除（若无其他成员则整行删）。

`RuleAction.cs`：
- 第 11 行 `public virtual ProFeature? RequiredFeature => null;` 删除
- 第 25 行 `public override ProFeature? RequiredFeature => ProFeature.RenameTemplate;` 删除

`RegexCondition.cs` 第 22 行 `public override ProFeature? RequiredFeature => ProFeature.RegularExpression;` 删除。

（保留 `virtual`/`override` 相关基类与派生关系其他逻辑不动）

- [ ] **Step 4: 修改 PreviewService.cs**

a. 第 16-17 行枚举成员删除：
```csharp
    /// <summary>规则需要 Pro 功能但未授权，跳过</summary>
    NeedsPro
```

b. 第 29-30 行 `BlockedFeature` 属性删除：
```csharp
    /// <summary>NeedsPro 时记录所需 Pro 功能中文名（/ 分隔）</summary>
    public string? BlockedFeature { get; init; }
```

c. 第 38 行签名改为（同时改第 37 行注释去掉 isAllowed 说明）：
```csharp
    public static List<PreviewEntry> Build(IReadOnlyList<Rule> rules, IEnumerable<FileEntry> files, DateTime now)
    {
        var previews = new List<PreviewEntry>();
```

d. 第 51-63 行 NeedsPro 拦截分支整体删除：
```csharp
            if (!RuleAllowed(rule, isAllowed))
            {
                var blocked = ...
                previews.Add(...);
                continue;
            }
```

e. 第 72-84 行 `RuleAllowed` 与 `FeatureName` 两个私有方法整体删除。

- [ ] **Step 5: 修改 Organizer.cs**

第 34-38 行删除：
```csharp
            if (p.Status == PreviewStatus.NeedsPro)
            {
                result.Skipped.Add(new OrganizeItem { Source = p.File.FullPath, Reason = $"需要 Pro 解锁（{p.BlockedFeature}）" });
                continue;
            }
```

- [ ] **Step 6: 修改四个测试文件**

`OrganizerGateTests.cs`：删除 `Execute_NeedsProGoesToSkipped` 整个用例（第 9-23 行，含 `[Fact]` 上方属性与写法的 `using FileTidy.Core.Models;` 保留——`Execute_TemplateErrorGoesToFailed` 仍用 `Rule`/`MoveAndRenameAction`）。

`RegexConditionTests.cs`：删除第 60-64 行：
```csharp
    [Fact]
    public void RequiredFeature_IsRegularExpression()
    {
        Assert.Equal(ProFeature.RegularExpression, new RegexCondition().RequiredFeature);
    }
```

`RuleActionTests.cs`：删除第 46-52 行 `RequiredFeature_MoveIsFree_RegexConditionIsPro` 整个用例。

`PreviewServiceTests.cs`：删除第 148-174 行两个用例 `Build_NeedsProWhenNotAllowed` 与 `Build_NeedsProJoinsMultipleFeatures`（其余 `Build(...)` 3 参调用与新签名兼容，无需改）。

- [ ] **Step 7: 构建与测试验证**

Run: `dotnet build` 与 `dotnet test tests/FileTidy.Tests`
Expected: 全部成功。若编译报残留引用错误（如某处仍引用 `ProFeature`/`RequiredFeature`/`NeedsPro`），grep 定位并删除对应引用后重跑。

- [ ] **Step 8: 提交**

```bash
git add -A
git commit -m "refactor: 删除 Core 授权体系（激活码/试用/Pro 门控）与 LicenseTool"
```

---

### Task 3: 清理 XAML 与主题资源（MainWindow、Colors、Controls）并适配资源测试

**Files:**
- Modify: `src/FileTidy.App/MainWindow.xaml`
- Modify: `src/FileTidy.App/Themes/Colors.xaml`、`src/FileTidy.App/Themes/Controls.xaml`
- Modify: `tests/FileTidy.Tests/AppResourcesLoadTests.cs`

**Interfaces:**
- Consumes: Task 1 已删除 `LicenseStateText`/`LicenseState`/`ActivationCode`/`ActivateResult`/`ActivateResultIsError`/`ActivateCommand` 等绑定源
- Produces: XAML 无任何授权相关绑定与资源键；`AppResourcesLoadTests` 键列表与资源一致

- [ ] **Step 1: MainWindow.xaml 修改**

a. 第 39 行页头副标题删除授权徽章 Run：
```xml
<Run Text="{Binding LicenseStateText, Mode=OneWay}"/><Run Text=" · "/>
```
保留 `<Run Text="{Binding EditorVms.Count, Mode=OneWay}"/><Run Text=" 条规则"/>`。

b. 第 100 行注释改为：
```xml
<!-- 预览表：行状态色（Moved 绿 / Conflict 琥珀 / TemplateError 红 / NoMatch 灰） -->
```

c. 第 112-114 行删除：
```xml
<DataTrigger Binding="{Binding Status}" Value="NeedsPro">
    <Setter Property="Background" Value="{StaticResource BrRowNeedsPro}"/>
</DataTrigger>
```

d. 第 200-203 行删除正则条件旁的 Pro 徽标（保留其后第 204 行起的文案行，将其 Margin 恢复为无左侧 Pro 徽标时的布局）：
```xml
<Border Style="{StaticResource ProBadge}">
    <TextBlock Style="{StaticResource ProBadgeText}"/>
</Border>
```
`<TextBlock Text="正则表达式（匹配完整文件名）" Style="{StaticResource FieldLabel}" Margin="6,0,0,0"/>` 的 Margin 改为 `"0,0,0,0"`（与第 204 行原样微调或直接删 Margin 属性）。

e. 第 232-234 行删除动作区 Pro 徽标：
```xml
<Border Style="{StaticResource ProBadge}" Margin="8,0,0,0" VerticalAlignment="Center">
    <TextBlock Style="{StaticResource ProBadgeText}"/>
</Border>
```

f. 第 278-321 行「账户 / 激活」整卡片删除（注释 `<!-- 账户 / 激活 -->` 到 `</Border>` 结束；其后是第 323 行 `<!-- 常规 -->`，注意补回 StackPanel 子元素间的层级——该卡片是设置 Tab StackPanel 的第一个子元素，删除后常规卡片成为第一个）。

g. 第 331-332 行删除：
```xml
<TextBlock Text="Pro 功能：正则条件、重命名模板、重复文件检测"
           Foreground="{StaticResource BrTextSecondary}" FontSize="11" Margin="0,12,0,0"/>
```

- [ ] **Step 2: Colors.xaml 修改**

删除：
```xml
    <SolidColorBrush x:Key="BrPro" Color="#D97706"/>
    <!-- Pro 徽标浅底（与 BrPro 同族） -->
    <SolidColorBrush x:Key="BrProSoft" Color="#FBF0E0"/>
```
（第 25-27 行）与第 32 行 `<SolidColorBrush x:Key="BrRowNeedsPro" Color="#FBF0E0"/>`。

- [ ] **Step 3: Controls.xaml 修改**

删除第 144-158 行整个「Pro 徽标」样式块（`<!-- Pro 徽标 -->` 注释 + `ProBadge` + `ProBadgeText`）。

- [ ] **Step 4: AppResourcesLoadTests.cs 修改**

`Colors_AllKeys_Exist`（第 72-81 行）键数组删除 `"BrPro"` 与 `"BrRowNeedsPro"`：
```csharp
"BrSuccess", "BrWarning", "BrError",
"BrRowMoved", "BrRowConflict", "BrRowTemplateError", "BrRowNoMatch",
```
`Controls_AllCoreKeys_Exist`（第 91-97 行）键数组删除 `"ProBadge"`：
```csharp
"CardBorder", "CardTitleText", "FieldLabel",
```

- [ ] **Step 5: 构建与测试验证**

Run: `dotnet build` 与 `dotnet test tests/FileTidy.Tests`
Expected: 全部成功；AppResourcesLoadTests 的资源键断言通过。

- [ ] **Step 6: 提交**

```bash
git add -A
git commit -m "refactor: 清理界面 Pro 徽标与账户激活卡片、授权相关资源"
```

---

### Task 4: 更新 README 并做残留关键词检查与全量验证

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: Task 1-3 已删除代码/资源/工具中的全部授权内容
- Produces: README 不再提 Pro/激活/试用

- [ ] **Step 1: README.md 修改**

a. 第 8-9 行删除 `（Pro）` 标注：
```markdown
- 正则表达式条件：正则匹配文件名，支持捕获组
- 重命名模板：`{name}` `{date:yyyyMMdd}` `{n}` `{1}` 等变量批量重命名
```

b. 第 16 行整个删除：`- Pro 解锁：离线激活码（14 天 / 20 次试用，试用耗尽后 Pro 功能拦截）`

c. 第 26-31 行「## Pro 与激活」整节删除（含其下 4 条列表行与前后空行调整）。

- [ ] **Step 2: 残留关键词检查**

Run:
```powershell
rg -i "VIP|vip|付费|收费|激活|授权|会员|Pro|license|trial|NeedsPro|ProBadge" src tests README.md FileTidy.sln --glob "!**/bin/**" --glob "!**/obj/**"
```
Expected: 无输出（`README.md` 与 `FileTidy.sln`、`src/`、`tests/` 中不再含授权词；`LICENSE`/`docs/` 历史文档不在检查范围）

- [ ] **Step 3: 全量构建与测试**

Run: `dotnet build` 与 `dotnet test tests/FileTidy.Tests`
Expected: 全部成功

- [ ] **Step 4: 提交**

```bash
git add -A
git commit -m "docs: README 移除 Pro 与激活说明，标注全部功能免费"
```

---

## 验收清单（全部任务完成后核对）

- `dotnet build` 通过
- `dotnet test tests/FileTidy.Tests` 通过
- `src/`、`tests/`、`README.md`、`FileTidy.sln` 无授权相关内容（grep 无命中）
- 正则条件、重命名模板、重复文件检测功能保留且无条件可用