# 去除收费项设计：全部功能免费化

日期：2026-08-10

## 背景与目标

FileTidy 此前包含一套 Pro 授权体系：激活码（RSA 签名）、14 天 / 20 次试用、Pro 功能门控（正则条件、重命名模板、重复文件检测）。本项目已转为 MIT 开源（见 `LICENSE`），故彻底删除收费相关代码，全部功能无条件免费使用。

**决策**：彻底删除授权体系（含生成工具、测试、UI、文档），而非保留代码仅放行。

## 影响范围总览

删除完整授权链：激活码生成（RSA 签名）→ 验证 → 试用计数 → Pro 功能门控 → UI 展示。业务功能本身（正则条件、重命名模板、重复文件检测）保留并全部无条件可用。

## Core 层（src/FileTidy.Core）

- 删除文件：`LicenseService.cs`、`LicenseCodec.cs`、`LicenseKeys.cs`、`ProFeature.cs`
- `AppPaths.cs`：删除 `LicenseFile`、`TrialFile` 路径属性
- 模型层：`FileCondition.cs`、`RuleAction.cs`、`RegexCondition.cs` 删除 `RequiredFeature` 虚属性（含 `MoveAndRenameAction` 的 `ProFeature.RenameTemplate` 覆盖、`RegexCondition` 的 `ProFeature.RegularExpression` 覆盖）
- `PreviewService.cs`：
  - 删除 `PreviewStatus.NeedsPro` 枚举值
  - 删除 `PreviewEntry.BlockedFeature` 属性
  - `Build` 签名删除 `Func<ProFeature, bool>? isAllowed` 参数及其缺省逻辑
  - 删除 `RuleAllowed`、`FeatureName` 私有方法
  - 其余（匹配、目标路径计算、模板渲染）逻辑不动

## App 层（src/FileTidy.App）

- `MainViewModel.cs`：
  - 删除 `_license` 字段与构造参数
  - 删除 `ActivateCommand`、`RefreshLicenseState`、`LicenseStateText`、`LicenseState`、`ActivationCode`、`ActivateResult`、`ActivateResultIsError`
  - `SetErrorDetailsWithProHint` 简化为普通错误明细（删除 Pro 拦截追加提示行）
  - 预览 `Build` 调用删除 isAllowed 参数；NeedsPro 相关分支（状态文案、跳过统计路径）删除
- `App.xaml.cs`：启动时不再构造/注入 `LicenseService`
- `MainWindow.xaml`：
  - 页头删授权状态徽章（`LicenseStateText` 绑定）
  - 预览表删 NeedsPro 行 DataTrigger 及 `BrRowNeedsPro` 引用
  - 删两处 ProBadge（正则条件、重命名模板旁）
  - 删「账户 / 激活」整卡片：标题、授权状态文字、激活码输入框、激活按钮、激活结果提示、「Pro 功能：正则条件、重命名模板、重复文件检测」说明文案
- `Themes/Colors.xaml`：删 `BrPro`、`BrProSoft`、`BrRowNeedsPro`
- `Themes/Controls.xaml`：删 `ProBadge`、`ProBadgeText` 样式
- `ViewModels/RuleEditorViewModel.cs`：注释中的「(Pro)」标记清理（纯注释，不改变行为）

## 测试（tests/FileTidy.Tests）

- 删除 `LicenseServiceTests.cs`
- `MainViewModelTests.cs`：改构造（不再传 license），删除试用/激活相关用例，适配 `LicenseStateText` 等属性删除
- `RegexConditionTests.cs`：删除 `RequiredFeature` 断言
- `RuleActionTests.cs`：删除/调整 `RequiredFeature_MoveIsFree_RegexConditionIsPro` 用例
- `PreviewServiceTests.cs`：`Build` 调用去掉 isAllowed 参数，删除 NeedsPro/BlockedFeature 相关断言

## 工具与解决方案

- 删除 `tools/FileTidy.LicenseTool/` 整个目录（含 `private_key.pem` 引用）
- `FileTidy.sln` 移除 `FileTidy.LicenseTool` 项目条目

## 文档

- `README.md`：删除「Pro 与激活」段落（功能列表保留：正则条件、重命名模板、重复文件检测不再标注为 Pro）
- 本设计文档不描述历史 spec 的修订（历史设计文档保留原样）

## 验证

- `dotnet build` 全部项目编译通过
- `dotnet test tests/FileTidy.Tests` 全部测试通过
- 检查无残留引用：`VIP|vip|付费|收费|激活|授权|license|License|ProFeature|NeedsPro|ProBadge` 等关键词在 `src/`、`tests/`、`README.md` 中无实质引用（`LICENSE` 文件本身的 MIT 文本除外）

## 非目标

- 不重构无关注逻辑（规则引擎、路径唯一化、文件夹监听等）
- 不删除历史设计文档 `docs/superpowers/specs/`
- 不新增任何替代的授权/订阅机制