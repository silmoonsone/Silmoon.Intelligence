# Silmoon.Intelligence

基于 **.NET 10** 的 Agent 与工具调用（Tool Calling）项目：在 **Silmoon.AI** 上封装 `AgentClient`、Hosting 编排（监管 + 主对话）、工作区与持久化。

**入口**：**控制台**为完整联调首选；**MAUI** 跨平台试验；**WinUI 3** 为 Windows 桌面客户端（导航 + 聊天，已接 Hosting）。三者均以 **`AddSilmoonIntelligence()`** 挂接同一套宿主逻辑（平台目录实现不同，见各客户端 `Services`）。

解决方案：**`Silmoon.Intelligence.slnx`**。具体 API 与行为以仓库内代码、`*.csproj` 为准。

---

## 仓库结构

### 类库（可被其他项目引用）

| 项目 | 说明 |
|------|------|
| `Silmoon.Intelligence` | 核心：`AgentClient`（如 `History` 与底层一致，为 **`IMessage`** 序列）、`AgentModelManager`、`AgentWorkspaceManager`、`ModelContextManager` 等 |
| `Silmoon.Intelligence.Tools` | 可复用工具：`GithubTool`、`CSharpTool`、**`WebSearchTool`**（阿里云 OpenSearch 网页搜索，见配置）等 |

### 宿主（服务注入与通用主机）

| 项目 | 说明 |
|------|------|
| `Silmoon.Intelligence.Hosting` | **依赖注入**：`IntelligenceService`（`BackgroundService`，监管 + 主对话双 `AgentClient`）、`ModelContextService`、`AgentWorkspaceService` 等；**`AddSilmoonIntelligence` / `AddSilmoonIntelligence<T>`** 注册宿主。`ModelContextService` 通过 **`InjectSupervisorTools` / `InjectMainChatTools`** 分别挂载工具集（含 **WebSearch**、Github、文件/命令等，以源码为准）。工作区含 **`workspace/system_prompts/*.md`**（构建时复制到 Hosting 输出），见 `Hosting/Extensions/ServiceCollectionExtension.cs` |

### 客户端应用

| 项目 | 说明 |
|------|------|
| `Silmoon.Intelligence.MauiClient` | .NET MAUI，`AddSilmoonIntelligence()` + `SilmoonConfigure`；`ISilmoonPlatformDirectoryService` 基于 **`FileSystem.AppDataDirectory`**。**当前为「可用」级别**，侧重联调与收集测试，非成熟产品化客户端 |
| `Silmoon.Intelligence.WinUIClient` | WinUI 3（`net10.0-windows10.0.19041.0`），泛型 **`Host`** + **`AddSilmoonIntelligence()`** + **`SilmoonConfigure`**；**`NavigationView`** + **`ChatPage`**（`MainChatAgentClient` 流式与工具事件）。平台目录为 **`AppContext.BaseDirectory`**。依赖 **Silmoon.Windows.WinUI3** 与 **Windows App SDK**。与 MAUI **同为演进中**，细节可能不完全对齐 |

### 测试与联调入口

| 项目 | 说明 |
|------|------|
| `Silmoon.Intelligence.ConsoleTesting` | 控制台：泛型 Host + `AddSilmoonIntelligence()` + `ConsoleService`，适合日常联调 |
| `Silmoon.Intelligence.WinFormTesting` | WinForms 壳与示例配置；**未接入** Hosting / 核心逻辑，可作占位或轻量 UI 试验 |

`slnx` 中「支撑框架」文件夹引用 **`../Silmoon.AI`**、**`../Silmoon.Maui`**、**`../Silmoon.Windows`**；完整构建需这些仓库与主仓库位于同一上级目录。

---

## 依赖布局

将下列仓库克隆到**同一上级目录**（与 `*.csproj` 中 `..\` 一致）：

```text
<parent>/
  Silmoon.Intelligence/
  Silmoon.AI/
  Silmoon.Windows/    # WinUI 需要
  Silmoon.Maui/       # MAUI 需要
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

```bash
dotnet run --project Silmoon.Intelligence.WinUIClient/Silmoon.Intelligence.WinUIClient.csproj
```

MAUI 仅构建某一目标时可用 `-f`，具体 TFM 以 `MauiClient.csproj` 为准。

**WinUI / MAUI** 需安装 **Windows App SDK**、**.NET MAUI** 工作负载及对应平台 SDK。无环境时可从解决方案移除 WinUI / MAUI 及相关外部引用后再 `dotnet build`。

---

## 配置

| 场景 | 配置文件（示例） |
|------|------------------|
| 控制台、WinForm、WinUI | 各项目目录下 `config.json`、`config.debug.json`（WinUI 已配置复制到输出目录） |
| MAUI | `MauiClient/Resources/Raw/config*.json` |

敏感或本机项使用 **`config.local.json`** / **`config.local.debug.json`**（已 `.gitignore`）。

解析逻辑与字段以 **`SilmoonConfigureServiceImpl.cs`** 为准。除既有 `modelProviders`、`defaultModel`、`systemPrompt`、`nativeClientDisableProxy` 等外，常见补充包括：

| 字段（JSON） | 说明 |
|--------------|------|
| **`aliyunOpenSearchKey`**（可选） | 阿里云 OpenSearch 网页搜索 **Bearer** 密钥。未配置时 **`WebSearch` 工具**会提示未配置，其余功能不受影响 |

示例 `config.json` 若结构与当前 `SilmoonConfigureServiceImpl` 不一致，需先对齐再启动。

### 工作区与 `system_prompts`

运行时宿主从 **`{WorkspaceDirectory}/system_prompts/`** 读取 `unified_agent_system.md`、`supervisor_agent_system.md` 等（见 `IntelligenceService` 与 `AgentWorkspaceService`）。**`WorkspaceDirectory`** 由各入口的 **`ISilmoonPlatformDirectoryService.AppDataDirectory`** 与 `workspace` 子目录组合而成。

若该目录下缺少上述文件，启动会失败。可从 **`Silmoon.Intelligence.Hosting/workspace/system_prompts`** 拷贝到当前应用工作区，或先构建 Hosting 再从输出目录同步。

主聊天历史持久化格式与 **`Silmoon.AI` 消息模型**一致（如 **`IMessage`** 反序列化）；恢复时若文件不含 system 消息，会尝试与内存中的 system 对齐，详见 **`AgentWorkspaceService`**。

---

## 项目内文档

- `Silmoon.Intelligence.Hosting/workspace/markdowns/`：开发与提示相关说明（与运行时实际注册工具集以代码为准）。

---

## 安全与网络

- **GithubTool** 需访问 GitHub 公开 API。
- **WebSearchTool** 需 **有效 `aliyunOpenSearchKey`**，并向**阿里云 OpenSearch** 开放搜索端点发起请求；密钥勿提交仓库，宜放 `config.local*.json`。
- **CSharpTool** 在宿主进程内执行脚本，**非**隔离沙箱。
- **CommandTool** 等与宿主环境强耦合，勿对不可信来源开放无限制调用。

---

## 许可证

见 **`LICENSE.txt`**。
