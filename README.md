# Silmoon.Intelligence

> **说明**：本 README 由 AI 辅助生成，具体行为、依赖与构建步骤请以仓库内实际代码、`*.csproj` 与 `Silmoon.Intelligence.slnx` 为准；如有出入以代码为准。

基于 **.NET 10** 的智能体（Agent）与 **工具调用（Tool Calling）** 实验项目：在 `Silmoon.AI` 之上提供对话客户端、宿主与一组可扩展工具，并包含 **控制台、WinUI 3、.NET MAUI** 等运行入口。

本仓库通过 **项目引用** 依赖同级目录下的 **`Silmoon.AI`**、**`Silmoon.Windows`**（WinUI）、**`Silmoon.Maui`**（MAUI）；完整编译前请按下文目录布局准备依赖仓库。

## 仓库结构

| 目录 / 项目 | 说明 |
|-------------|------|
| `Silmoon.Intelligence` | 核心：`AgentClient`、`AgentModelManager` 及对 `Silmoon.AI` 的封装 |
| `Silmoon.Intelligence.Tools` | 工具实现：`CSharpTool`（Roslyn 脚本，含安全规则与超时）、`AgentModelTool`（多模型 / 静态 Agent 委派）等 |
| `Silmoon.Intelligence.Hosting` | 宿主：`ToolFunctionService`（注入 File / Command / Wait / CSharp / Memory / AgentModel 等工具）、`ConsoleService`、`ClientService`、`SilmoonConfigureServiceImpl` |
| `Silmoon.Intelligence.ConsoleTesting` | 控制台入口，引用 Hosting，适合命令行联调 |
| `Silmoon.Intelligence.WinUIClient` | WinUI 3 客户端：应用内 `Host`，使用 `ClientService` 与 UI；依赖 `Silmoon.Windows.WinUI3` |
| `Silmoon.Intelligence.MauiClient` | MAUI 客户端：`AppShell` 主窗口；`MauiProgram` 中注册 Hosting（含 `ConsoleService`）；依赖 `Silmoon.Maui` |
| `Silmoon.Intelligence.WinFormTesting` | WinForms 占位项目（保留 `config*.json`，便于后续扩展；当前无 Hosting / 核心集成） |

解决方案：**`Silmoon.Intelligence.slnx`**（Visual Studio 或 `dotnet build` 均可）。

## 依赖仓库与目录布局

**强烈建议**将下列四个仓库克隆到**同一上级目录**，与 `*.csproj` / `*.slnx` 中的相对路径一致：

```text
<workspace>/
  Silmoon.Intelligence/
  Silmoon.AI/
  Silmoon.Windows/
  Silmoon.Maui/
```

```bash
git clone https://github.com/silmoonsone/Silmoon.Intelligence.git
git clone https://github.com/silmoonsone/Silmoon.AI.git
git clone https://github.com/silmoonsone/Silmoon.Windows.git
git clone https://github.com/silmoonsone/Silmoon.Maui.git
```

- **`Silmoon.AI`**：本仓库内核心与工具项目引用 **`Silmoon.AI/Silmoon.AI.csproj`**。
- **`Silmoon.Windows`**：`WinUIClient` 引用 **`Silmoon.Windows.WinUI3`**。
- **`Silmoon.Maui`**：`MauiClient` 引用 **`Silmoon.Maui/Silmoon.Maui.csproj`**。

缺少任一依赖时，完整解决方案可能无法编译；可自行从 `slnx` 中移除 WinUI / MAUI 等项目后再构建（见下文）。

## 构建与运行

在 `Silmoon.Intelligence` 目录下：

```bash
dotnet build Silmoon.Intelligence.slnx
```

控制台联调示例：

```bash
dotnet run --project Silmoon.Intelligence.ConsoleTesting/Silmoon.Intelligence.ConsoleTesting.csproj
```

WinUI 与 MAUI 需本机安装 **Windows App SDK**、**.NET MAUI 工作负载** 及对应目标平台 SDK，以 Visual Studio 工作负载说明为准。

### 可选：暂不构建 WinUI / MAUI

若无对应环境，可从解决方案移除后再构建（**仍需已克隆 `Silmoon.AI`**）：

```bash
dotnet sln Silmoon.Intelligence.slnx remove Silmoon.Intelligence.WinUIClient/Silmoon.Intelligence.WinUIClient.csproj
dotnet sln Silmoon.Intelligence.slnx remove Silmoon.Intelligence.MauiClient/Silmoon.Intelligence.MauiClient.csproj
dotnet build Silmoon.Intelligence.slnx
```

## 配置

各入口目录中的 **`config.json`**、**`config.debug.json`** 为示例。  
本机或敏感配置请使用 **`config.local.json`**、**`config.local.debug.json`**（已由 `.gitignore` 排除，不会提交）。

## 环境与安全说明

- 目标框架以各 `.csproj` 为准：核心多为 **`net10.0`**；WinUI 为 **`net10.0-windows10.0.19041.0`**；MAUI 为多目标（Android / iOS / Mac Catalyst / Windows 等）。
- **`CSharpTool` 在宿主进程内执行脚本**，带时间与输出上限及静态安全规则，**并非隔离沙箱**；请勿对不可信来源开放无限制调用。

## 许可证

见仓库根目录 **`LICENSE.txt`**。
