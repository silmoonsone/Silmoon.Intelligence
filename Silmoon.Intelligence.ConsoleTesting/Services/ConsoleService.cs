using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Silmoon.AI.Models;
using Silmoon.AI.OpenAI.Models.Enums;
using Silmoon.AI.OpenAI.Models;
using Silmoon.Extensions;
using Silmoon.Intelligence.Hosting.Services;
using Silmoon.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.ConsoleTesting.Services
{
    public class ConsoleService : BackgroundService
    {
        IHostApplicationLifetime ApplicationLifetime { get; set; }
        IntelligenceService IntelligenceService { get; set; }
        ILogger<ConsoleService> Logger { get; set; }
        public ConsoleService(IHostApplicationLifetime applicationLifetime, IntelligenceService intelligenceService, ILogger<ConsoleService> logger)
        {
            ApplicationLifetime = applicationLifetime;
            IntelligenceService = intelligenceService;
            Logger = logger;
        }


        private async Task SupervisorAgentClient_OnToolCallsStart(ToolCallParameter[] toolCallParameters)
        {
            Console.WriteLineWithColor($"[TOOL CALLS] {string.Join(',', toolCallParameters.Select(x => x.FunctionName))}", ConsoleColor.Yellow);
        }
        private async Task<ToolCallResult[]> SupervisorAgentClient_OnToolCallsFinish(ToolCallParameter[] toolCallParameters, ToolCallResult[] toolCallResults)
        {
            Console.WriteLineWithColor($"[TOOL CALLS RESULTS] {string.Join(", ", toolCallParameters.Select(x => $"{x.FunctionName}: {toolCallResults.FirstOrDefault(y => y.Parameter.FunctionName == x.FunctionName)?.Result.State}"))}", ConsoleColor.Yellow);
            return toolCallResults;
        }
        private async Task SupervisorAgentClient_OnStreamOutputCompleted(Result result)
        {
            Console.WriteLine();
            Console.WriteLine("stop reason: " + result.FinishReason);
        }
        private async Task SupervisorAgentClient_OnStreamOutput(StateSet<bool, ChatCompletionsChunk> chunkState)
        {
            if (chunkState.State)
            {
                chunkState.Data.Choices.Each(x =>
                {
                    if (x.Delta?.ToolCalls is not null) Console.Write(".");
                    else
                    {
                        Console.WriteWithColor(x?.Delta?.GetThinking(), ConsoleColor.DarkGray);
                        Console.WriteWithColor(x?.Delta?.Content, ConsoleColor.DarkGray);
                    }
                });
            }
            else Console.WriteLineWithColor(chunkState.Message, ConsoleColor.Red);
        }


        private async Task AgentClient_OnToolCallsStart(ToolCallParameter[] toolCallParameters)
        {
            Console.WriteLineWithColor($"[TOOL CALLS] {string.Join(',', toolCallParameters.Select(x => x.FunctionName))}", ConsoleColor.Yellow);
        }
        private async Task<ToolCallResult> AgentClient_OnToolCallInvoke(ToolCallParameter toolCallParameter, ToolCallResult toolCallResult)
        {
            return null;
        }
        private async Task AgentClient_OnToolExecuting(string functionName, ToolCallParameter toolCallParameter)
        {
            Console.WriteLineWithColor($"[Tool Executing] ({functionName}) is executing.", ConsoleColor.Cyan);
        }
        private async Task AgentClient_OnToolExecuted(string functionName, ToolCallParameter toolCallParameter, ToolCallResult toolCallResult)
        {
            if (toolCallResult is not null)
            {
                if (toolCallResult.Result.State)
                    Console.WriteLineWithColor($"[Tool Executed] ({functionName}) executed with result: State: {toolCallResult.Result.State}, Message: {toolCallResult.Result.Message}", ConsoleColor.Cyan);
                else
                    Console.WriteLineWithColor($"[Tool Executed] ({functionName}) executed with result: State: {toolCallResult.Result.State}, Message: {toolCallResult.Result.Message}", ConsoleColor.Red);
            }
            else
                Console.WriteLineWithColor($"[Tool Executed] ({functionName}) executed with no any result", ConsoleColor.Red);
        }
        private async Task<ToolCallResult[]> AgentClient_OnToolCallsFinish(ToolCallParameter[] toolCallParameters, ToolCallResult[] toolCallResults)
        {
            Console.WriteLineWithColor($"[TOOL CALLS RESULTS] {string.Join(", ", toolCallParameters.Select(x => $"{x.FunctionName}: {toolCallResults.FirstOrDefault(y => y.Parameter.FunctionName == x.FunctionName)?.Result.State}"))}", ConsoleColor.Yellow);
            return toolCallResults;
        }
        private async Task AgentClient_OnStreamOutput(StateSet<bool, ChatCompletionsChunk> chunkState)
        {
            if (chunkState.State)
            {
                chunkState.Data.Choices.Each(x =>
                {
                    if (x.Delta?.ToolCalls is not null) Console.Write(".");
                    else
                    {
                        Console.WriteWithColor(x?.Delta?.GetThinking(), ConsoleColor.DarkGray);
                        Console.WriteWithColor(x?.Delta?.Content, ConsoleColor.White);
                    }
                });
            }
            else Console.WriteLineWithColor(chunkState.Message, ConsoleColor.Red);
        }
        private async Task AgentClient_OnStreamOutputCompleted(Result result)
        {
            Console.WriteLine();
            Console.WriteLine("stop reason: " + result.FinishReason);
        }

        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            IntelligenceService.ReadyResetEvent.WaitOne();

            IntelligenceService.DefaultChatAgentClient.OnToolCallsStart += AgentClient_OnToolCallsStart;
            IntelligenceService.DefaultChatAgentClient.OnToolCallInvoke += AgentClient_OnToolCallInvoke;
            IntelligenceService.DefaultChatAgentClient.OnToolExecuting += AgentClient_OnToolExecuting;
            IntelligenceService.DefaultChatAgentClient.OnToolExecuted += AgentClient_OnToolExecuted;
            IntelligenceService.DefaultChatAgentClient.OnToolCallsFinish += AgentClient_OnToolCallsFinish;
            IntelligenceService.DefaultChatAgentClient.OnStreamOutput += AgentClient_OnStreamOutput;
            IntelligenceService.DefaultChatAgentClient.OnStreamOutputCompleted += AgentClient_OnStreamOutputCompleted;

            IntelligenceService.SupervisorAgentClient.OnToolCallsStart += SupervisorAgentClient_OnToolCallsStart;
            IntelligenceService.SupervisorAgentClient.OnToolCallsFinish += SupervisorAgentClient_OnToolCallsFinish;
            IntelligenceService.SupervisorAgentClient.OnStreamOutput += SupervisorAgentClient_OnStreamOutput;
            IntelligenceService.SupervisorAgentClient.OnStreamOutputCompleted += SupervisorAgentClient_OnStreamOutputCompleted;

            Logger.LogInformation($"恢复{IntelligenceService.DefaultChatAgentClient.History.Count}条聊天信息，已就绪。");
            Logger.LogInformation("@clear 清理聊天历史，@exit 退出应用，@getsystemprompt 获取系统提示，@back 回退消息历史，@stat 查看消息历史数量，@save 保存聊天历史，@restore 恢复聊天历史");
            await Task.Delay(100, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                Console.Write(Role.User + ": ");
                string input = await Console.In.ReadLineAsync(stoppingToken);
                if (input.IsNullOrEmpty())
                {
                    if (input is null)
                    {
                        Console.WriteLine("Input terminated");
                        break;
                    }
                    else continue;
                }
                if (input.FirstOrDefault() == '@')
                {
                    string command = input[1..].Trim();
                    switch (command)
                    {
                        case "clear":
                            IntelligenceService.DefaultChatAgentClient.ClearHistory();
                            Console.WriteLine("历史被清空.");
                            break;
                        case "exit":
                            ApplicationLifetime.StopApplication();
                            break;
                        case "getsystemprompt":
                            Console.WriteLine(IntelligenceService.DefaultChatAgentClient.NativeChatClient.SystemPrompt);
                            break;
                        case "back":
                            Console.WriteLine($"当前消息历史数量：{IntelligenceService.DefaultChatAgentClient.NativeChatClient.MessageHistory.Count}");
                            IntelligenceService.DefaultChatAgentClient.RollbackHistory();
                            Console.WriteLine($"回退后消息历史数量：{IntelligenceService.DefaultChatAgentClient.NativeChatClient.MessageHistory.Count}");
                            Console.WriteLine();
                            break;
                        case "stat":
                            Console.WriteLine($"当前消息历史数量：{IntelligenceService.DefaultChatAgentClient.NativeChatClient.MessageHistory.Count}");
                            break;
                        case "save":
                            Console.WriteLine($"Save reuslt: {IntelligenceService.SaveChatState(IntelligenceService.DefaultChatAgentClient.Id).ToJsonString()}");
                            break;
                        case "restore":
                            Console.WriteLine($"Restore result: {IntelligenceService.RestoreChatState(IntelligenceService.DefaultChatAgentClient.Id).ToJsonString()}");
                            Console.WriteLine($"Last message:");
                            Console.WriteLine($"user: {IntelligenceService.DefaultChatAgentClient.NativeChatClient.MessageHistory.LastOrDefault(x => x.Role == Role.User)?.GetContent()}");
                            Console.WriteLine($"assistant: {IntelligenceService.DefaultChatAgentClient.NativeChatClient.MessageHistory.LastOrDefault(x => x.Role == Role.Assistant)?.GetContent()}");
                            break;
                        case "getsystem":
                            Console.WriteLine(IntelligenceService.DefaultChatAgentClient.NativeChatClient.SystemPrompt);
                            break;
                        default:
                            Console.WriteLine($"Unknown command: {command}");
                            break;
                    }
                }
                else
                {
                    Console.Write(Role.Assistant + ": ");
                    var result = await IntelligenceService.Chat(input);
                    if (result.Usage is not null)
                        Console.WriteLine($"total tokens: {result.Usage.TotalTokens:N0}, prompt tokens: {result.Usage.PromptTokens:N0}, completion tokens: {result.Usage.CompletionTokens:N0}");
                    Console.WriteLine();
                }
            }
        }
    }
}

