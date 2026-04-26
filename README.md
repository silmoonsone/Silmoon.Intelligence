# Silmoon.Intelligence

`Silmoon.Intelligence` 是一个基于 .NET 的智能体与工具调用实验项目，包含核心能力、宿主层以及多个客户端/测试端（Console、WinForm、WinUI、MAUI）。

项目当前以研发迭代为主，`WinUI` 与 `MAUI` 客户端仍在开发中，文档会优先保证你可以先把核心能力跑起来。

## 项目结构

- `Silmoon.Intelligence`：核心能力层
- `Silmoon.Intelligence.Tools`：工具层（如 C# 脚本执行工具等）
- `Silmoon.Intelligence.Hosting`：宿主层
- `Silmoon.Intelligence.ConsoleTesting`：控制台测试入口
- `Silmoon.Intelligence.WinFormTesting`：WinForms 测试入口
- `Silmoon.Intelligence.WinUIClient`：WinUI 客户端（开发中）
- `Silmoon.Intelligence.MauiClient`：MAUI 客户端（开发中）

## 依赖说明

本仓库包含跨仓库项目引用。**若要完整编译解决方案，强烈建议同时克隆以下 3 个仓库到同级目录：**

- `Silmoon.AI`（核心 AI 能力依赖）
- `Silmoon.Windows`（`WinUI` 客户端依赖）
- `Silmoon.Maui`（`MAUI` 客户端依赖）

如果缺少以上任一仓库，完整编译可能失败或需要手动移除对应项目引用。

## 快速开始（建议路径）

### 1) 克隆仓库

```bash
git clone https://github.com/silmoonsone/Silmoon.Intelligence.git
```

### 2) 准备外部依赖仓库（同级目录，推荐完整方式）

建议目录结构：

```text
<workspace>/
  Silmoon.Intelligence/
  Silmoon.AI/
  Silmoon.Windows/
  Silmoon.Maui/
```

推荐直接执行以下克隆命令（与本仓库保持同级）：

```bash
git clone https://github.com/silmoonsone/Silmoon.AI.git
git clone https://github.com/silmoonsone/Silmoon.Windows.git
git clone https://github.com/silmoonsone/Silmoon.Maui.git
```

### 3) 完整编译解决方案（推荐）

```bash
dotnet build Silmoon.Intelligence.slnx
```

### 4) 可选：仅编译核心（跳过 WinUI/MAUI）

如果你暂时不准备 `WinUI`/`MAUI` 相关环境或外部依赖，可以先移除这两个项目后再构建（此方式仍需要 `Silmoon.AI`）：

```bash
dotnet sln Silmoon.Intelligence.slnx remove Silmoon.Intelligence.WinUIClient/Silmoon.Intelligence.WinUIClient.csproj
dotnet sln Silmoon.Intelligence.slnx remove Silmoon.Intelligence.MauiClient/Silmoon.Intelligence.MauiClient.csproj
dotnet build Silmoon.Intelligence.slnx
```

你也可以直接单独构建/运行测试项目：

```bash
dotnet build Silmoon.Intelligence.ConsoleTesting/Silmoon.Intelligence.ConsoleTesting.csproj
dotnet run --project Silmoon.Intelligence.ConsoleTesting/Silmoon.Intelligence.ConsoleTesting.csproj
```

## 配置说明

各测试/客户端项目内包含 `config.json` / `config.debug.json` 等配置文件作为示例入口。  
推荐在本地使用 `config.local.json`、`config.local.debug.json` 覆盖配置（这些文件已在 `.gitignore` 中忽略，不会提交）。

## 平台与要求

- .NET SDK：`net10.0`（预览/较新版本）
- Windows 客户端：
  - WinForms：`net10.0-windows`
  - WinUI：`net10.0-windows10.0.19041.0` + Windows App SDK
- MAUI：需具备对应平台工作负载与开发环境

## 当前状态

- 核心、宿主与测试端持续可用并在迭代
- WinUI/MAUI 客户端处于开发中，后续会补充完整编译与使用说明

## 许可证

本项目使用仓库中的 `LICENSE.txt`。