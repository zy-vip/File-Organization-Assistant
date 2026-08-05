# AGENTS.md

FileTidy（文件整理助手）：Windows 桌面文件自动整理工具。C# / .NET 8 / WPF，面向简体中文用户。

## 构建与测试

- 构建：`dotnet build`（单命令构建全部项目）
- 测试：`dotnet test tests/FileTidy.Tests`
- 发布：`dotnet publish src/FileTidy.App -c Release -r win-x64 -o dist`（自包含单文件 exe，AssemblyName 是 `FileTidy`）

## 项目结构

- `src/FileTidy.Core`（net8.0）：无 UI 依赖的领域核心——规则匹配、扫描、预览、执行、操作日志、互斥队列 `EngageQueue`、文件夹监听 `FolderWatcher`。核心逻辑必须放这里，独立可测。
- `src/FileTidy.App`（net8.0-windows / WPF）：界面 + 托盘（Hardcodet.NotifyIcon.Wpf）。自行实现 MVVM（`ObservableObject`），**禁止**引入 CommunityToolkit.Mvvm。ViewModel 只调用 Core，不含文件逻辑。
- `tests/FileTidy.Tests`（net8.0-windows / xUnit）：引用 Core 与 App。

## 约定与易错点

- 所有代码注释、提交信息、界面文案一律使用简体中文；提交信息用 conventional 风格（`feat:` / `chore:` / `docs:`）。
- **不得用 PowerShell 5.1 的 `Set-Content`/`Out-File` 写含中文的文件**——它会破坏 UTF-8。修改文件必须用编辑器工具。
- 测试用真实临时目录（`Directory.CreateTempSubdirectory`）操作真实文件；跨卷移动测试通过 `subst` 映射虚拟盘符（仅 Windows 可用）。
- 配置存 `%AppData%\FileTidy\config.json`（含规则 + 全局设置）；操作日志存 `%AppData%\FileTidy\operations\`（保留 10 份，需撤销）。注意日志文件名按时间戳排序生效。
- 冲突自动追加序号：`报告.pdf` → `报告(1).pdf`（`PathHelper.GetUniquePath`）。
- 条件模型 `FileCondition` 用 System.Text.Json `[JsonDerivedType]` 多态序列化，新增条件类型时必须注册派生类型，否则序列化丢失。
- 设计文档：`docs/superpowers/specs/`；实现计划：`docs/superpowers/plans/`。