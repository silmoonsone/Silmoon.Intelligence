using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using Silmoon.AI.Models;
using Silmoon.AI.Models.OpenAI.Enums;
using Silmoon.AI.Models.OpenAI.Models;
using Silmoon.AI.OpenAI;
using Silmoon.Extensions;
using Silmoon.Extensions.Hosting.Interfaces;
using Silmoon.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.Hosting.Services
{
    public class ConsoleService : IHostedService
    {
        AgentClient AgentClient { get; set; }
        ContextManagerService ContextManagerService { get; set; }
        SilmoonConfigureServiceImpl SilmoonConfigureService { get; set; }
        IHostApplicationLifetime ApplicationLifetime { get; set; }

        public ConsoleService(ISilmoonConfigureService silmoonConfigureService, ContextManagerService contextManagerService, IHostApplicationLifetime applicationLifetime)
        {
            SilmoonConfigureService = silmoonConfigureService as SilmoonConfigureServiceImpl;
            ApplicationLifetime = applicationLifetime;
            ContextManagerService = contextManagerService;

            AgentClient = new AgentClient(SilmoonConfigureService.DefaultProvider, SilmoonConfigureService.DefaultModelName, "主管", "监管全局工作", disableProxy: true);
            ContextManagerService.InjectTools(AgentClient.NativeChatClient);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            AgentClient.OnToolCallsStart += AgentClient_OnToolCallsStart;
            AgentClient.OnToolCallInvoke += AgentClient_OnToolCallInvoke;
            AgentClient.OnToolExecuting += AgentClient_OnToolExecuting;
            AgentClient.OnToolExecuted += AgentClient_OnToolExecuted;
            AgentClient.OnToolCallsFinish += AgentClient_OnToolCallsFinish;

            AgentClient.OnStreamOutput += AgentClient_OnStreamOutput;
            AgentClient.OnStreamOutputCompleted += AgentClient_OnStreamOutputCompleted;
            _ = StartConsole();
            await Task.CompletedTask;
        }

        private async Task<ToolCallResult> AgentClient_OnToolCallInvoke(ToolCallParameter toolCallParameter, ToolCallResult toolCallResult)
        {
            //Console.WriteLineWithColor($"[TOOL CALL] {toolCallParameter.FunctionName}", ConsoleColor.Cyan);

            if (toolCallParameter.FunctionName == "ToolCallTestTool")
                return await Task.FromResult(ToolCallResult.Create(toolCallParameter, true.ToStateSet<string>("这是一个工具调用环境测试，正常！")));
            else return null;
        }
        private async Task AgentClient_OnToolCallsStart(ToolCallParameter[] toolCallParameters)
        {
            Console.WriteLineWithColor($"[TOOL CALLS] {string.Join(',', toolCallParameters.Select(x => x.FunctionName))}", ConsoleColor.Yellow);
        }
        private Task AgentClient_OnToolExecuting(string functionName, ToolCallParameter toolCallParameter)
        {
            Console.WriteLineWithColor($"[Tool Executing] ({functionName}) is executing.", ConsoleColor.Cyan);
            return Task.CompletedTask;
        }
        private Task AgentClient_OnToolExecuted(string functionName, ToolCallParameter toolCallParameter, ToolCallResult toolCallResult)
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
            return Task.CompletedTask;
        }
        private Task<ToolCallResult[]> AgentClient_OnToolCallsFinish(ToolCallParameter[] toolCallParameters, ToolCallResult[] toolCallResults)
        {
            Console.WriteLineWithColor($"[TOOL CALLS RESULTS] {string.Join(", ", toolCallParameters.Select(x => $"{x.FunctionName}: {toolCallResults.FirstOrDefault(y => y.Parameter.FunctionName == x.FunctionName)?.Result.State}"))}", ConsoleColor.Yellow);
            return Task.FromResult(toolCallResults);
        }
        private Task AgentClient_OnStreamOutput(StateSet<bool, Chunk> chunkState)
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
            return Task.CompletedTask;
        }
        private Task AgentClient_OnStreamOutputCompleted(Result result)
        {
            Console.WriteLine();
            Console.WriteLine("stop reason: " + result.FinishReason);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
        }

        public async Task StartConsole()
        {
            await Task.Run(async () =>
            {
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
                                AgentClient.NativeChatClient.ResetHistory();
                                Console.WriteLine("Message history cleared.");
                                break;
                            case "exit":
                                ApplicationLifetime.StopApplication();
                                break;
                            default:
                                Console.WriteLine($"Unknown command: {command}");
                                break;
                        }
                    }
                    else
                    {
                        Console.Write(Role.Assistant + ": ");
                        await AgentClient.Chat(input);
                        Console.WriteLine();
                    }
                }
            });
        }
    }
}
