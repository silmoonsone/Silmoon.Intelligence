# Silmoon.Intelligence

基于 **.NET 10** 的 Agent 与工具调用（Tool Calling）项目：在 **Silmoon.AI** 上封装 `AgentClient`、Hosting 编排、工作区与按会话持久化。

**运行时模型**：**监管 Agent（Supervisor）** + **多个主对话 Agent（按 `Guid` 区分）**；启动时从工作区扫描并恢复历史会话，无历史则自动新建。用户消息可由宿主附加 `<time>` 等内部前缀（展示时会剥离，见 `IntelligenceService.GetUserRealInput`）。

**入口**：**控制台**适合命令行联调；**WinUI 3** 为当前功能最完整的 Windows 桌面客户端（多会话列表 + 聊天 + Markdown + 工具指示器）；**MAUI** 可运行，侧重跨平台试验。三者通过 **`AddSilmoonIntelligence()`** 挂接同一套 Hosting（各客户端 **`ISilmoonPlatformDirectoryService`** 实现不同，见各项目 `Services`）。

解决方案：**`Silmoon.Intelligence.slnx`**。具体 API 以仓库内代码、`*.csproj` 为准。

---

## 仓库结构

### 类库（可被其他项目引用）

| 项目 | 说明 |
|------|------|
| `Silmoon.Intelligence` | 核心：`AgentClient`（**`Guid Id`**、**`AgentState`** 为 **Agent 状态**（含 **`Topic`**、聊天记录等元数据）、`History` 为 **`IMessage`**；主对话可 **`enableThinking`**）、`AgentModelManager`、`AgentWorkspaceManager`、`ModelContextManager` 等 |
| `Silmoon.Intelligence.Tools` | 可复用工具：`GithubTool`、`CSharpTool`、**`WebSearchTool`**（阿里云 OpenSearch，见配置）等 |

### 宿主（服务注入与通用主机）

| 项目 | 说明 |
|------|------|
| `Silmoon.Intelligence.Hosting` | **`IntelligenceService`**（`BackgroundService`）：**`SupervisorAgentClient`**；**`AgentClients`**；**`DefaultChatAgentClient`**（单会话入口 **`Chat(input)`** 等）。**`SaveChatState` / `RestoreChatState`** 持久化完整 **`AgentState`**（**`LastAt`** 排序启动恢复）；对话后可 **`GenerateAgentTopic`**（由监管 Agent 生成会话标题）。**`ReadyResetEvent`**；工具注入见 **`ModelContextService`**、**`ServiceCollectionExtension.cs`** |

### 客户端应用

| 项目 | 说明 |
|------|------|
| `Silmoon.Intelligence.MauiClient` | .NET MAUI，单会话联调；走 **`DefaultChatAgentClient`**（与控制台同类）。平台目录 **`FileSystem.AppDataDirectory`**。功能与 WinUI 未必对齐 |
| `Silmoon.Intelligence.WinUIClient` | WinUI 3 桌面客户端：**多会话**（列表以 **`Topic`** 展示，新建/切换/删除/复制；**`ChatPage`** 按当前 **`AgentClient`**，**不依赖** **`DefaultChatAgentClient`**）。**Markdown**、流式正文与**思考/推理**内容分区显示、工具指示器；主对话 Agent 默认开启思考能力（UI 开关待完善）。平台目录 **`AppContext.BaseDirectory`**；依赖 **Silmoon.Windows.WinUI3**、**Windows App SDK** |

### 测试与联调入口

| 项目 | 说明 |
|------|------|
| `Silmoon.Intelligence.ConsoleTesting` | 控制台联调：单会话，**`DefaultChatAgentClient`**、**`Chat(input)`**；`@save` / `@restore` 对应 **`SaveChatState` / `RestoreChatState`**（见 **`ConsoleService`**） |
| `Silmoon.Intelligence.WinFormTesting` | WinForms 壳与示例配置；**未接入** Hosting / 核心逻辑 |

`slnx` 中「支撑框架」引用 **`../Silmoon.AI`**、**`../Silmoon.Maui`**、**`../Silmoon.Windows`**；完整构建需与主仓库位于同一上级目录。

---

## 依赖布局

```text
<parent>/
  Silmoon.Intelligence/
  Silmoon.AI/
  Silmoon.Windows/    # WinUI
  Silmoon.Maui/       # MAUI
```

缺少依赖时，可从 `slnx` 中移除不需要的客户端或外部工程引用后再构建。

---

## 构建与运行

```bash
dotnet build Silmoon.Intelligence.slnx
dotnet run --project Silmoon.Intelligence.ConsoleTesting/Silmoon.Intelligence.ConsoleTesting.csproj
dotnet run --project Silmoon.Intelligence.WinUIClient/Silmoon.Intelligence.WinUIClient.csproj
```

MAUI 单平台构建可用 `-f`，TFM 以 `MauiClient.csproj` 为准。需 **Windows App SDK** / **.NET MAUI** 等工作负载；无环境时可从解决方案移除对应客户端后再构建。

---

## 配置

| 场景 | 配置文件（示例） |
|------|------------------|
| 控制台、WinForm、WinUI | 各项目目录下 `config.json`、`config.debug.json`（WinUI 复制到输出目录） |
| MAUI | `MauiClient/Resources/Raw/config*.json` |

敏感项使用 **`config.local.json`** / **`config.local.debug.json`**（已 `.gitignore`）。字段以 **`SilmoonConfigureServiceImpl.cs`** 为准。

| 字段（JSON） | 说明 |
|--------------|------|
| **`aliyunOpenSearchKey`**（可选） | 阿里云 OpenSearch **Bearer** 密钥；未配置时 **WebSearch** 工具会提示未配置 |

### 工作区

| 路径（相对 `{WorkspaceDirectory}`） | 说明 |
|-----------------------------------|------|
| **`system_prompts/`** | `unified_agent_system.md`、`supervisor_agent_system.md` 等；**启动时必读**，缺失会导致启动失败。可从 **`Hosting/workspace/system_prompts`** 或 Hosting 构建输出拷贝 |
| **`main_agent_memories/`** | 各会话 **`agent_{guid}_chat_history.json`**，整份 **`AgentState`**（Id、Provider、Model、**`Topic`**、**`IMessage[]`**、**`CreatedAt` / `LastAt`** 等） |
| **`markdowns/`** | 开发与工具说明文档 |

**`WorkspaceDirectory`** = 各入口 **`ISilmoonPlatformDirectoryService.AppDataDirectory`** + **`workspace`**（控制台/WinUI 多为程序目录下；MAUI 为应用数据目录）。

---

## 项目内文档

- `Silmoon.Intelligence.Hosting/workspace/markdowns/`：工具与开发说明（与运行时实际注册工具集以代码为准）。

---

## 安全与网络

- **GithubTool** 需访问 GitHub 公开 API。
- **WebSearchTool** 需有效 **`aliyunOpenSearchKey`**，并向阿里云 OpenSearch 端点发起请求；密钥勿提交仓库。
- **CSharpTool** 在宿主进程内执行脚本，**非**隔离沙箱。
- **CommandTool** 等与宿主环境强耦合，勿对不可信来源开放无限制调用。

---

## 许可证

见 **`LICENSE.txt`**。
