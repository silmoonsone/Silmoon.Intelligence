# Silmoon.Intelligence

基于 **.NET 10** 的 Agent 与工具调用（Tool Calling）项目：在 **Silmoon.AI** 上封装 `AgentClient`、Hosting 编排与会话持久化。**持久化**正在从 `workspace` 文件机制迁移至 **LiteDB**（已接入 `Core`，业务数据将逐步入库）；`workspace` 仍在使用，后续会逐步淡出。

**运行时模型**：**监管 Agent（Supervisor）** + **多个主对话 Agent（按字符串 `Id` 区分，兼容既有 Guid 字符串）**；启动时从 **`workspace`** 扫描并恢复 **`AgentState`**（无历史则新建）。**LiteDB**（**`Core`**，配置 **`connectionString`**）为长期存储方向，与 **`workspace`** 并存过渡中。用户消息可由宿主附加 `<time>` 等前缀（展示见 **`IntelligenceService.GetUserRealInput`**）。

**入口**：**Avalonia** 是新的跨平台桌面客户端方向，复用现有 Hosting / Agent 核心并提供多会话、Markdown、思考内容、工具执行指示与 Token 用量展示；**控制台**适合命令行联调；**WinUI 3** 为 legacy Windows 客户端，短期可作为功能参考但不再作为主要维护方向；**MAUI** 已进入过期/实验状态，不建议继续投入新功能。各入口通过 **`AddSilmoonIntelligence()`** 挂接同一套 Hosting（各客户端 **`ISilmoonPlatformDirectoryService`** 实现不同，见各项目 `Services`）。

解决方案：**`Silmoon.Intelligence.slnx`**。具体 API 以仓库内代码、`*.csproj` 为准。

---

## 仓库结构

### 类库（可被其他项目引用）

| 项目 | 说明 |
|------|------|
| `Silmoon.Intelligence` | 核心：`AgentClient`（**字符串 `Id`**、**`AgentState`** 为 **Agent 状态**（含 **`Topic`**、`NativeHistory` 等元数据）、`History` 为 **`NativeMessageCollection`**；主对话可 **`enableThinking`**）、`AgentModelManager`、`AgentWorkspaceManager`、`ModelContextManager` 等 |
| `Silmoon.Intelligence.Tools` | 可复用工具：`GithubTool`、`CSharpTool`、**`WebSearchTool`**（阿里云 OpenSearch，见配置）等 |

### 宿主（服务注入与通用主机）

| 项目 | 说明 |
|------|------|
| `Silmoon.Intelligence.Hosting` | **`IntelligenceService`**（`BackgroundService`）：**`SupervisorAgentClient`**；**`AgentClients`**；**`DefaultChatAgentClient`**（单会话入口 **`Chat(input)`** 等）。当前 **`SaveChatState` / `RestoreChatState`** 仍写 **`workspace`** 下 JSON；启动时会先升级 `main_agent_memories/` 旧记忆文件到当前结构；**`Core`**（**LiteDB**，**`connectionString`**）已接入，**后续持久化将迁入 LiteDB**。**`GenerateAgentTopic`**、**`ReadyResetEvent`**；工具注入见 **`ModelContextService`**、**`ServiceCollectionExtension.cs`** |

### 客户端应用

| 项目 | 说明 |
|------|------|
| `Silmoon.Intelligence.Client` | **Avalonia 桌面客户端（主维护方向）**：跨平台客户端基础实现；复用 `Silmoon.Intelligence.Hosting` 与现有 Agent 核心，支持多会话恢复/切换/删除、Markdown 正文展示、思考内容、工具调用动态指示、工具 token 流计数、Token 用量展示。该客户端基础实现主要由 Codex 生成与迭代，尚未经过详细人工代码审查，继续开发或发布前需重点复核。配置文件沿用 WinUI `config*.json` 并复制到输出目录；平台目录见 `Services/SilmoonPlatformDirectoryServiceImpl.cs` |
| `Silmoon.Intelligence.MauiClient` | **已过期/实验性客户端**：.NET MAUI 单会话联调；走 **`DefaultChatAgentClient`**（与控制台同类）。平台目录 **`FileSystem.AppDataDirectory`**。功能不会继续主动追齐 Avalonia |
| `Silmoon.Intelligence.WinUIClient` | **Legacy Windows 客户端**：功能曾最完整，可作为 Avalonia 迁移参考。支持 **多会话**（**`Topic`** 列表，新建/切换/删除/复制；**`ChatPage`** 按当前 **`AgentClient`**，**不依赖** **`DefaultChatAgentClient`**）、**Markdown**、思考/推理分区、工具指示器、**Token 用量**展示。主对话默认 **`enableThinking`**。平台目录 **`AppContext.BaseDirectory`**；依赖 **Silmoon.Windows.WinUI3**、**Windows App SDK**。后续只建议维护必要修复 |

### 测试与联调入口

| 项目 | 说明 |
|------|------|
| `Silmoon.Intelligence.ConsoleTesting` | 控制台联调：单会话，**`DefaultChatAgentClient`**、**`Chat(input)`**；`@save` / `@restore` 等见 **`ConsoleService`**（回复后输出 Token 用量） |
| `Silmoon.Intelligence.WinFormTesting` | WinForms 壳与示例配置；**未接入** Hosting / 核心逻辑 |

`slnx` 中「支撑框架」引用上述外部仓库（**`Silmoon.AI`**、**`Silmoon.Data`**、**`Silmoon.Maui`**、**`Silmoon.Windows`** 等）；克隆方式见下文 **依赖布局**。

---

## 依赖布局

本解决方案通过 **项目引用** 依赖多个**外部仓库**，路径以 `*.csproj` / `*.slnx` 中的 `..\` 为准。这些仓库均在 GitHub 用户 **[silmoonsone](https://github.com/silmoonsone)** 下，请克隆到**与 `Silmoon.Intelligence` 同一上级目录**（文件夹名与下表一致）：

```text
<parent>/
  Silmoon.Intelligence/    ← 本仓库
  Silmoon.AI/
  Silmoon.Data/
  Silmoon.Windows/         # 构建 WinUI 时需要
  Silmoon.Maui/            # 构建 MAUI 时需要
```

示例（在 `<parent>` 目录下执行）：

```bash
git clone https://github.com/silmoonsone/Silmoon.Intelligence.git
git clone https://github.com/silmoonsone/Silmoon.AI.git
git clone https://github.com/silmoonsone/Silmoon.Data.git
git clone https://github.com/silmoonsone/Silmoon.Windows.git
git clone https://github.com/silmoonsone/Silmoon.Maui.git
```

仅构建控制台 / Hosting 核心时，**至少需要 `Silmoon.AI`**；Avalonia / LiteDB（**`Core`**）需要 **Silmoon.Data**；WinUI / MAUI 分别还需要 **Silmoon.Windows**、**Silmoon.Maui**。缺少依赖时，可从 `slnx` 中移除不需要的客户端或外部工程引用后再构建。

---

## 构建与运行

```bash
dotnet build Silmoon.Intelligence.slnx
dotnet run --project Silmoon.Intelligence.ConsoleTesting/Silmoon.Intelligence.ConsoleTesting.csproj
dotnet run --project Silmoon.Intelligence.Client/Silmoon.Intelligence.Client.csproj
dotnet run --project Silmoon.Intelligence.WinUIClient/Silmoon.Intelligence.WinUIClient.csproj
```

Avalonia 客户端是当前桌面主入口。WinUI / MAUI 构建分别需要 **Windows App SDK** / **.NET MAUI** 等工作负载；MAUI 单平台构建可用 `-f`，TFM 以 `MauiClient.csproj` 为准。无对应环境时，可从解决方案移除不需要的客户端后再构建。

---

## 配置

| 场景 | 配置文件（示例） |
|------|------------------|
| 控制台、WinForm、WinUI | 各项目目录下 `config.json`、`config.debug.json`（WinUI 复制到输出目录） |
| Avalonia | 复用 `WinUIClient/config*.json`，由 `Silmoon.Intelligence.Client.csproj` 链接并复制到输出目录 |
| MAUI | `MauiClient/Resources/Raw/config*.json` |

敏感项使用 **`config.local.json`** / **`config.local.debug.json`**（已 `.gitignore`）。字段以 **`SilmoonConfigureServiceImpl.cs`** 为准。

| 字段（JSON） | 说明 |
|--------------|------|
| **`aliyunOpenSearchKey`**（可选） | 阿里云 OpenSearch **Bearer** 密钥；未配置时 **WebSearch** 工具会提示未配置 |
| **`connectionString`**（可选） | **LiteDB** 连接串（如 `Filename=data.db;Mode=Shared`）；**`Core`** 使用，**Agent 状态等持久化将逐步迁入**（WinUI 示例配置已包含） |
| **`defaultModel`** | 默认模型选择；`defaultProvider` 对应 `modelProviders[].providerName`，`defaultModelName` 对应该提供商下的模型名 |
| **`modelProviders`** | 模型提供商数组；每项会反序列化为 `ModelProvider`，包含 `providerName`、`apiUrl`、`apiKey`、`models` 等 |
| **`modelProviders[].apiKind`** | Native API 类型；当前使用 `Chat` / `Responses` / `Authropic`，旧值 `OpenAIChatCompletions` 等已不再兼容 |
| **`nativeClientDisableProxy`**（可选） | 创建 Native Client 时是否禁用系统代理 |

### 持久化与工作区（过渡中）

**当前**：**`AgentState`** 等主要仍通过 **`workspaces/main_agent_memories/`** 下 JSON 读写（**`SaveChatState` / `RestoreChatState`**，启动按 **`LastAt`** 恢复）。`AgentWorkspaceService` 创建时会先执行记忆文件升级：旧文件名 **`agent_{id}_chat_history.json`** 会迁移为 **`agentState-{id}.json`**，文件名中的 dashed Guid 会转换为无 `-` 的字符串 Id；随后旧字段 **`ChatHistory`** 会迁移为 **`NativeHistory`**，消息里的旧 **`hash` / `Hash`** 会迁移为当前 **`id`**，旧消息 `$type` 会迁移为 **`Silmoon.AI.OpenAI.Models.NativeMessage*`** 类型名。正常读取流程只面向当前结构，后续结构变更也应继续收敛到这个启动迁移入口。**`system_prompts/`**、**`markdowns/`** 等同理，仍依赖 **`workspace`** 目录。

**方向**：**LiteDB**（`Core` + `connectionString`）为长期存储；`workspace` 机制会逐步过时，迁移完成前两者并存。路径与字段以当前代码为准。

| 路径（相对 WorkspaceDirectory） | 说明 |
|--------------------------------|------|
| **`system_prompts/`** | 系统提示词 Markdown；**启动时必读**。可从 **`Hosting/workspaces/system_prompts`** 或 Hosting 构建输出拷贝 |
| **`main_agent_memories/`** | 各会话 **`agentState-{id}.json`**（**`AgentState`**）；**过渡期仍用，后续迁至 LiteDB** |
| **`markdowns/`** | 开发与工具说明文档 |

**WorkspaceDirectory** = 各入口 `ISilmoonPlatformDirectoryService.AppDataDirectory` + `workspaces`（控制台/WinUI 多为程序目录下；MAUI 为应用数据目录）。

---

## 项目内文档

- `Silmoon.Intelligence.Hosting/workspaces/markdowns/`：工具与开发说明（与运行时实际注册工具集以代码为准）。

---

## 安全与网络

- **GithubTool** 需访问 GitHub 公开 API。
- **WebSearchTool** 需有效 **`aliyunOpenSearchKey`**，并向阿里云 OpenSearch 端点发起请求；密钥勿提交仓库。
- **CSharpTool** 在宿主进程内执行脚本，**非**隔离沙箱。
- **CommandTool** 等与宿主环境强耦合，勿对不可信来源开放无限制调用。

---

## 许可证

见 **`LICENSE.txt`**。
