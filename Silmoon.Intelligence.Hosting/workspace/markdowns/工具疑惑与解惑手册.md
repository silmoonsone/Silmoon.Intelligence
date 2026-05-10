# 工具疑惑与解惑手册

> 目标读者：AI 大模型。生成于 2026-05-11，基于 Windows / .NET 10 / 阿里云 qwen3.6-plus 环境实测。
>
> **⚠️ 重要：** 本文档列出的工具集是**本文作者所在宿主环境注册的全部工具**。不同 AI 大模型实例可能拥有不同的工具配置——你有的工具本文可能没写，本文写的工具你可能没有。请以你实际可调用的工具列表为准。本文仅用于消除**你已拥有但心存疑虑的工具**的疑惑。

---

## 1. Command_StatefulExecute 系列

**状态保持：真实有效。** `cd`、环境变量等在同一个 instanceId 会话中持续保持。

**输出机制：**
- `Execute` → 返回**全部历史输出**
- `GetOutput` → 返回**自上次 GetOutput 以来的增量**（实测很少需要，Execute 的结果就够了）

**"同一交互最多一个 Stateful 调用"**：一次 tool calls 块中最多只能出现一个 `Command_Stateful*` 调用。多步必须串行。

**instanceId：** 同任务用同一个 id，不同任务用不同 id。id 错了工具会提示。任务结束调 `Close`。

**典型流程：** `Execute(id, cmd1)` → (等返回) → `Execute(id, cmd2)` → ... → `Close(id)`

---

## 2. CSharp_Run

**`using var` 可能报 CS1002** → 改用 `using (...) { }`。

**默认命名空间：** `System`、`System.IO`、`System.Linq`、`System.Collections.Generic`。其他需手动 using。

**HTTP 请求可用：** `HttpClient` + `await` 可直接写。

**安全边界（直接拒绝执行）：**
- `Process.Start` / P/Invoke / `unsafe` / `Assembly.LoadFrom` / `Environment.Exit`
- 注册表写入
- `WebClient.DownloadFile` 等整块下载

**大文件读取：** 必须流式 + 设硬上限（建议 ≤ 5MB）。否则拦截。

**第三方 NuGet 包默认不可用。** 用 `System.Text.Json` 代替 Newtonsoft.Json。

---

## 3. AgentModel 系列

**前置：** 必须先调 `GetAgentModelProviders()`，确认你有啥模型。providerName/modelName **大小写敏感**，`enable=false` 的模型不可用。

**CallSingletonAgent：** 有状态。独立任务前调 `ResetSingletonAgentHistory`。

**WorkerAgent 生命周期（实测通过）：** `Create → Call → Remove`。Worker 只有 `Test_ToolCallTest` 工具（除非你的环境另有配置）。name 唯一，不能同名并行创建。忘了删不破坏系统但持续占用。

---

## 4. GithubTool 系列

**前提：** 仓库必须**公开**。私有/不存在返回 not found。

**格式：** `"owner/repo"`。**ref 参数：** 不传则自动检测默认分支。

---

## 5. Memory 系列

⚠️ **未实测。** `ApplyMemory` 清空全部历史，不可逆。没有用户明确要求不要碰。

---

## 6. 通用规则

**并发限制：** 同 Stateful 会话 / 同 Worker name / 同 Singleton (provider,model) → 串行。同文件 write 后 read → 串行。其余可并行。

**大小写敏感：** providerName、modelName、Worker name、GitHub repository。**不敏感：** os、terminalType。
