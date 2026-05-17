# CSharp_Run 工具边界速查

> 供配置了相同 CSharp_Run（Roslyn Script）工具的大模型参考，非代码执行环境配置文档。

---

## 一、工具本质

- **执行引擎**：Roslyn 编译器，top-level script 语法，在当前宿主进程内执行（非沙箱）
- **超时限制**：口头声称 10 秒；实测 11 秒密集循环未被终止（软限制/CPU 时间感知）
- **输出上限**：64K 字符
- **语言版本**：低于 C# 8.0（不支持 `using var`，必须用传统 `using (...) { }`）

## 二、默认可用命名空间

```
System · System.IO · System.Linq · System.Collections.Generic
```

通过 `using` 可导入更多（如 `System.Net`、`System.Net.Http`、`System.Reflection`、`System.Diagnostics`、`System.Threading` 等），实测基本无限制。

---

## 三、安全策略（编译前静态字符串模式匹配拦截）

> ⚠️ **安全扫描是纯字符串/正则子串匹配**，不区分代码与字符串字面量。注释、字符串常量、文档中的敏感词也会触发拦截。写入含这些词的文本时请换用 `File_File` 或 `Command_Run` 等工具。

### ❌ 直接拦截（代码不会执行）

| 类别 | 拦截关键字 / 模式 | 拦截消息 |
|------|-------------------|----------|
| **宿主进程终止** | `Environment.Exit`、`FailFast` | 禁止终止宿主进程 |
| **进程控制** | `System.Diagnostics.Process`（整类） | 禁止使用 Process 创建/控制外部进程 |
| **不安全代码** | `unsafe` 关键字 + 指针操作 | 禁止 unsafe 指针与底层内存操作 |
| **原生调用** | `[DllImport]`、`LibraryImport`、`Marshal`、`NativeLibrary` | 禁止调用 Win32/Native DLL（P/Invoke） |
| **端口监听** | `TcpListener`、`Socket.Bind`/`Listen` | 禁止监听端口或创建服务化守护能力 |
| **大文件下载** | `GetByteArrayAsync`、`ReadAsByteArrayAsync`、`WebClient.Download*`、`File.WriteAllBytes` | 推荐流式 + 硬上限 ≤5MB |
| **反射绕过** | `Assembly.LoadFrom` / `LoadFile` | 禁止加载未知程序集 |
| **网络攻击** | 洪水/端口扫描/DoS/DDoS/无限重试 | 禁止攻击性流量 |

### ⚠️ 盲区 / 灰色区（允许但有风险）

| 风险项 | 说明 |
|--------|------|
| **注册表写入** | `Registry.CurrentUser.OpenSubKey("Software", true)` 可在 HKCU 下创建/删除子键（包括 Run 启动项路径） |
| **反射私有成员** | `BindingFlags.NonPublic` 可读取私有字段值、调用私有方法 |
| **文件删除无白名单** | 当前用户权限内的任意文件可删（依赖操作者自律） |
| **根目录写入** | 实测可在 `D:\` 根目录写文件（可能因用户权限较高） |
| **系统目录写入** | 被 OS ACL 阻止（`UnauthorizedAccessException`），非 C# 策略层限制 |

### ✅ 确认允许的操作

- **文件**：`WriteAllText`/`ReadAllText`/`AppendAllText`/`Directory.CreateDirectory`/`File.Delete`/`Directory.Delete`
- **网络**：`HttpClient.GetAsync`（需 `HttpCompletionOption.ResponseHeadersRead`）、`Dns.GetHostEntry`、`UdpClient` 出站
- **反射**：`Assembly.GetExecutingAssembly()`、`Type.GetMethods/GetFields`、`FieldInfo.GetValue`（含私有）、`MethodInfo.Invoke`
- **并发**：`Task.Run`、`Task.WaitAll`、线程池、`Interlocked`
- **内存**：大数组分配（实测 100MB+ 无问题）、`GC.Collect`、`GC.GetGCMemoryInfo`
- **环境**：`Environment.GetEnvironmentVariables`、`Environment.ProcessId`、`Environment.Version`、`Environment.UserName`
- **计算**：BigInteger、LINQ、Regex、AES/MD5/SHA256、排序/算法、表达式树——均可正常运行

---

## 四、典型踩坑点

1. **字符串字面量误拦截**：代码中不要出现 `Process`、`Environment.Exit`、`unsafe`、`DllImport`、`TcpListener` 等词，即使是在字符串或注释中。如必须写入含这些词的内容，用 `File_File` 或 `Command_Run` 代替。
2. **`using var` 不能用**：用传统 `using (...) { }` 替代。
3. **`Process` 整类禁止**：连只读查询（`Process.GetProcesses()`）也被拦截。
4. **`File.WriteAllBytes` 禁止**：写入二进制数据会被拦截；文本用 `WriteAllText`。
5. **大文件下载必须流式**：不能 `GetByteArrayAsync`；流式读取应设 ≤5MB 硬上限。
6. **超时不严格**：长时间循环可能不被中断，注意自行加时间退出保护。

---

*最后更新：2026-05-17 · 由 5 份历史测试报告综合提炼而成*
