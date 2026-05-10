using Microsoft.Extensions.Hosting;
using Silmoon.AI.Models;
using Silmoon.AI.Models.OpenAI.Enums;
using Silmoon.AI.Models.OpenAI.Models;
using Silmoon.Extensions;
using Silmoon.Intelligence.Hosting.Services;
using Silmoon.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.ConsoleTesting.Services
{
    public class ConsoleService : IHostedService
    {
        IHostApplicationLifetime ApplicationLifetime { get; set; }
        IntelligenceService IntelligenceService { get; set; }
        public ConsoleService(IHostApplicationLifetime applicationLifetime, IntelligenceService intelligenceService)
        {
            ApplicationLifetime = applicationLifetime;
            IntelligenceService = intelligenceService;
            IntelligenceService.MainChatAgentClient.OnToolCallsStart += AgentClient_OnToolCallsStart;
            IntelligenceService.MainChatAgentClient.OnToolCallInvoke += AgentClient_OnToolCallInvoke;
            IntelligenceService.MainChatAgentClient.OnToolExecuting += AgentClient_OnToolExecuting;
            IntelligenceService.MainChatAgentClient.OnToolExecuted += AgentClient_OnToolExecuted;
            IntelligenceService.MainChatAgentClient.OnToolCallsFinish += AgentClient_OnToolCallsFinish;
            IntelligenceService.MainChatAgentClient.OnStreamOutput += AgentClient_OnStreamOutput;
            IntelligenceService.MainChatAgentClient.OnStreamOutputCompleted += AgentClient_OnStreamOutputCompleted;

            IntelligenceService.SupervisorAgentClient.OnToolCallsStart += SupervisorAgentClient_OnToolCallsStart;
            IntelligenceService.SupervisorAgentClient.OnToolCallsFinish += SupervisorAgentClient_OnToolCallsFinish;
            IntelligenceService.SupervisorAgentClient.OnStreamOutput += SupervisorAgentClient_OnStreamOutput;
            IntelligenceService.SupervisorAgentClient.OnStreamOutputCompleted += SupervisorAgentClient_OnStreamOutputCompleted;

            ApplicationLifetime.ApplicationStarted.Register(async () => await StartConsoleInput());
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
        }
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
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
        private async Task SupervisorAgentClient_OnStreamOutput(StateSet<bool, Chunk> chunkState)
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
        private async Task AgentClient_OnStreamOutput(StateSet<bool, Chunk> chunkState)
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


        public Task StartConsoleInput()
        {
            return Task.Run(async () =>
            {
                IntelligenceService.ReadyResetEvent.WaitOne();
                await Task.Delay(500);
                while (true)
                {
                    Console.Write(Role.User + ": ");
                    string input = Console.ReadLine();
                    if (input.IsNullOrEmpty()) continue;
                    if (input.FirstOrDefault() == '@')
                    {
                        string command = input[1..].Trim();
                        switch (command)
                        {
                            case "clear":
                                IntelligenceService.MainChatAgentClient.NativeChatClient.ResetHistory();
                                Console.WriteLine("历史被清空.");
                                break;
                            case "exit":
                                ApplicationLifetime.StopApplication();
                                break;
                            case "getsystemprompt":
                                Console.WriteLine(IntelligenceService.MainChatAgentClient.NativeChatClient.SystemPrompt);
                                break;
                            case "back":
                                Console.WriteLine($"当前消息历史数量：{IntelligenceService.MainChatAgentClient.NativeChatClient.MessageHistory.Count}");
                                IntelligenceService.MainChatAgentClient.NativeChatClient.RollbackHistory();
                                Console.WriteLine($"回退后消息历史数量：{IntelligenceService.MainChatAgentClient.NativeChatClient.MessageHistory.Count}");
                                Console.WriteLine();
                                break;
                            case "stat":
                                Console.WriteLine($"当前消息历史数量：{IntelligenceService.MainChatAgentClient.NativeChatClient.MessageHistory.Count}");
                                break;
                            case "save":
                                Console.WriteLine($"Save reuslt: {IntelligenceService.SaveChatHistory().ToJsonString()}");
                                break;
                            case "restore":
                                Console.WriteLine($"Restore result: {IntelligenceService.RestoreChatHistory().ToJsonString()}");
                                Console.WriteLine($"Last message:");
                                Console.WriteLine($"user: {IntelligenceService.MainChatAgentClient.NativeChatClient.MessageHistory.LastOrDefault(x => x.Role == Role.User)?.Content}");
                                Console.WriteLine($"assistant: {IntelligenceService.MainChatAgentClient.NativeChatClient.MessageHistory.LastOrDefault(x => x.Role == Role.Assistant)?.Content}");
                                break;
                            //case "re":
                            //    IntelligenceService.AgentClient.NativeChatClient.CompletionsStreamAsync(IntelligenceService.AgentClient.NativeChatClient.MessageHistory);
                            //    break;
                            default:
                                Console.WriteLine($"Unknown command: {command}");
                                break;
                        }
                    }
                    else
                    {
                        Console.Write(Role.Assistant + ": ");
                        await IntelligenceService.Input(input);
                        Console.WriteLine();
                    }
                }
            });
        }
    }
}
