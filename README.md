# 文件整理助手（FileTidy）

Windows 桌面文件自动整理工具：配置规则，一键把下载/桌面等杂乱文件夹整理得井井有条。整理前可预览，整理后可撤销。

## 功能

- 按扩展名 / 文件名关键词 / 修改时间（N 天前）自动分类
- 规则按顺序匹配，先命中先处理，单个文件单轮只处理一次
- 整理前预览，冲突自动追加序号（报告.pdf → 报告(1).pdf）
- 一次整理一份可撤销日志，支持逐次回退（保留最近 10 份）
- 可选托盘常驻 + 自动整理（新文件到达 3 秒后自动执行）
- 可选开机自启
- 配置存于 %AppData%\FileTidy\config.json

## 使用

1. 下载 release 中的 FileTidy.exe（自包含单文件，无需安装任何运行时）
2. 新建规则：填源文件夹、条件（扩展名/关键词/N 天前）、目标文件夹
3. 点「预览结果」确认 → 点「立即整理」
4. 点「撤销上次整理」还原

## 开发

```powershell
dotnet build
dotnet test tests/FileTidy.Tests
dotnet publish src/FileTidy.App -c Release -r win-x64 -o dist
```

## 许可证

待定