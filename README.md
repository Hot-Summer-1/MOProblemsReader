# MOProblemsReader

数学竞赛题目阅读器：从题库仓库同步题目，按「分类 → 赛事 → 年份」浏览。

## 功能

- 树形浏览题库：国内 / 国外 / 大学 / 原创 → 赛事 → 年份
- 一键更新题库，随时获取最新题目
- 可修改题源地址，可导出诊断数据
- 界面强调色跟随 Windows 系统设置
- 「关于」窗口显示当前版本与版权信息

## 使用

1. 首次启动会自动下载题库，请保持联网；下载期间会显示进度窗口，且无法关闭。
2. 需要最新题目时，使用菜单「文件 → 更新」或按 `Ctrl+U`。
3. 「文件 → 设置」中可修改题源地址、日志级别，或导出诊断数据。
4. 「文件 → 退出」或 `Alt+F4` 退出程序。

## 运行要求

- Windows 10 2004 或更高版本
- .NET 10 运行时

## 从源码构建

需要 .NET 10 SDK（解决方案为 `.slnx` 格式），以及 Windows 10 SDK 10.0.19041 目标包（在 Visual Studio 中随「.NET 桌面开发」工作负载安装）。

```powershell
dotnet build MOProblemsReader.slnx
```

首次构建会从 NuGet 还原以下依赖，需要联网：

- MaterialDesignThemes 5.2.1（仅主程序，用于加载指示器样式）
- LibGit2Sharp 0.32.0 / LibGit2Sharp.NativeBinaries 2.0.324（题库同步，自带 git 原生库，无需另外安装 Git）
- Serilog 4.4.0、Serilog.Enrichers.Process 3.0.0、Serilog.Sinks.Debug 3.0.0、Serilog.Sinks.File 7.0.0（日志）

仓库未提供 `global.json`、`nuget.config` 或 `Directory.Packages.props`，无需额外配置包源。

## 题库

默认题库仓库为 <https://github.com/Hot-Summer-1/MOProblems.git>。

在设置中更换题源地址后，下次启动会重新下载题库。

## 日志与反馈

遇到问题时，可在「设置」中点击「导出诊断数据」，把 zip 日志包发给开发者以便定位。日志位于 `%LOCALAPPDATA%\MOProblemsReader\log`。

## 已知限制

- 双击年份查看题目内容的功能尚未完成，目前只会弹出该年份所属的赛事与分类提示。

## 许可证

本项目基于 MIT License 发布，Copyright (c) 2026 Hot-Summer-1。
