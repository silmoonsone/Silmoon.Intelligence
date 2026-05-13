# Silmoon.Intelligence

基于 **.NET 10** 的 Agent 与工具调用（Tool Calling）项目：在 **Silmoon.AI** 上封装 `AgentClient`、Hosting 编排（监管 + 主对话）、工作区与持久化；**控制台**为完整联调入口，**MAUI** 为可运行试验客户端，**WinUI** 尚为空壳。

解决方案：**`Silmoon.Intelligence.slnx`**。具体 API 与行为以仓库内代码、`*.csproj` 为准。

---

## 仓库结构

### 类库（可被其他项目引用）

| 项目 | 说明 |
|------|------|
| `Silmoon.Intelligence` | 核心：`AgentClient`、`AgentModelManager`、`AgentWorkspaceManager`、`ModelContextManager` 等 |
| `Silmoon.Intelligence.Tools` | 本仓库内工具实现（如 `GithubTool`、`CSharpTool` 等） |

### 宿主（服务注入与通用主机）

| 项目 | 说明 |
|------|------|
| `Silmoon.Intelligence.Hosting` | **依赖注入**：注册 `IntelligenceService`、`ModelContextService`、`AgentWorkspaceService` 等；**通用主机侧**扩展（`AddSilmoonIntelligence` / `AddSilmoonIntelligence<T>`）供各入口挂接。内含双 Agent、工具注入、工作区与 `workspace/system_prompts` 等资源，详见 `Hosting/Extensions/ServiceCollectionExtension.cs` |

### 客户端应用

| 项目 | 说明 |
|------|------|
| `Silmoon.Intelligence.MauiClient` | .NET MAUI，已接 `AddSilmoonIntelligence()` 与 Hosting。**当前仅为「可用」级别**，侧重联调与**收集测试**，非成熟产品化客户端 |
| `Silmoon.Intelligence.WinUIClient` | WinUI 3。**当前工程基本为空壳**（无与 MAUI 对等的完整 Hosting / 聊天集成），其他开发者加入前**勿预期**已有可用业务 UI；后续以本仓库演进为准 |

### 测试与联调入口

| 项目 | 说明 |
|------|------|
| `Silmoon.Intelligence.ConsoleTesting` | 控制台：泛型 Host + `AddSilmoonIntelligence()` + `ConsoleService`，适合联调 |
| `Silmoon.Intelligence.WinFormTesting` | WinForms 壳与示例配置；**未接入** Hosting / 核心逻辑，可作占位或轻量 UI 试验 |

`slnx` 中「支撑框架」文件夹还引用 **`../Silmoon.AI`**、**`../Silmoon.Maui`**、**`../Silmoon.Windows`**；完整构建需这些仓库与主仓库位于同一上级目录。

---

## 依赖布局

将下列仓库克隆到**同一上级目录**（与 `*.csproj` 中 `..\` 一致）：

```text
<parent>/
  Silmoon.Intelligence/
  Silmoon.AI/
  Silmoon.Windows/    # WinUI 需要
  Silmoon.Maui/         # MAUI 需要
```

缺少依赖时，可从 `slnx` 中移除不需要的客户端或外部工程引用后再构建。

---

## 构建与运行

在仓库根目录：

```bash
dotnet build Silmoon.Intelligence.slnx
```

```bash
dotnet run --project Silmoon.Intelligence.ConsoleTesting/Silmoon.Intelligence.ConsoleTesting.csproj
```

MAUI 仅构建某一目标时可用 `-f`，例如 Windows 目标以 `MauiClient.csproj` 中为准。

**WinUI / MAUI** 需安装 **Windows App SDK**、**.NET MAUI** 工作负载及对应平台 SDK。无环境时可从解决方案移除 WinUI / MAUI 及相关外部引用后再 `dotnet build`。

---

## 配置

| 场景 | 配置文件（示例） |
|------|------------------|
| 控制台、WinForm、WinUI | 各项目目录下 `config.json`、`config.debug.json` |
| MAUI | `MauiClient/Resources/Raw/config*.json` |

敏感或本机项使用 **`config.local.json`** / **`config.local.debug.json`**（已 `.gitignore`）。

配置字段与 JSON 结构以 **`SilmoonConfigureServiceImpl.cs`** 为准；示例 `config.json` 若结构过旧需先对齐再启动。

---

## 项目内文档

- `Silmoon.Intelligence.Hosting/workspace/markdowns/`：开发与提示相关说明（与运行时实际注册工具集以代码为准）。

---

## 安全与网络

- **GithubTool** 需访问 GitHub 公开 API。
- **CSharpTool** 在宿主进程内执行脚本，**非**隔离沙箱。
- 已注册的命令类工具与宿主环境强耦合，勿对不可信来源开放无限制调用。

---

## 许可证

见 **`LICENSE.txt`**。
