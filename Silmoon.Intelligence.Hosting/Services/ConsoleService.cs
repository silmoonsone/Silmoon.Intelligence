using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using Silmoon.AI.Handlers;
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
        ToolFunctionService ToolFunctionService { get; set; }
        SilmoonConfigureServiceImpl SilmoonConfigureService { get; set; }
        IHostApplicationLifetime ApplicationLifetime { get; set; }

        public ConsoleService(ISilmoonConfigureService silmoonConfigureService, ToolFunctionService toolFunctionService, IHostApplicationLifetime applicationLifetime)
        {
            SilmoonConfigureService = silmoonConfigureService as SilmoonConfigureServiceImpl;
            ApplicationLifetime = applicationLifetime;
            ToolFunctionService = toolFunctionService;

            AgentClient = new AgentClient(SilmoonConfigureService.DefaultProvider, SilmoonConfigureService.DefaultModelName);
            ToolFunctionService.InjectTools(AgentClient.NativeChatClient);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            AgentClient.ToolCallStartHandler = ToolCallStartHandler;
            AgentClient.ToolCallCompletedHandler = ToolCallCompletedHandler;
            AgentClient.StreamOutputAction = StreamOutputAction;
            AgentClient.StreamOutputFinishedAction = StreamOutputFinishedAction;

            _ = StartConsole();
            await Task.CompletedTask;
        }
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
        }

        async Task<List<ToolCallResult>> ToolCallStartHandler(ToolCallParameter[] toolCallParameters, ConcurrentDictionary<string, ToolCallResult> toolCallResults)
        {
            List<ToolCallResult> results = [];

            foreach (var parameter in toolCallParameters)
            {
                var functionName = parameter.FunctionName;
                var parameters = parameter.Parameters;

                Console.WriteLineWithColor($"[TOOL CALL] {functionName}", ConsoleColor.Yellow);
                switch (functionName)
                {
                    case "ToolCallTestTool":
                        results.Add(ToolCallResult.Create(parameter, true.ToStateSet<string>($"这是一个工具调用环境测试，正常！")));
                        break;
                    default:
                        break;
                }
            }
            return results;
        }
        async Task<ConcurrentDictionary<string, ToolCallResult>> ToolCallCompletedHandler(ConcurrentDictionary<string, ToolCallResult> toolCallResults)
        {
            foreach (var toolCallResult in toolCallResults.Values)
            {
                if (toolCallResult.Result.State) Console.WriteLineWithColor($"[TOOL RESULT] State: {toolCallResult.Result.State}, Message: {toolCallResult.Result.Message}", ConsoleColor.Cyan);
                else Console.WriteLineWithColor($"[TOOL RESULT] State: {toolCallResult.Result.State}, Message: {toolCallResult.Result.Message}", ConsoleColor.Red);
            }
            return await Task.FromResult(toolCallResults);
        }
        void StreamOutputAction(StateSet<bool, Chunk> chunk)
        {
            if (chunk.State)
            {
                chunk.Data.Choices.Each(x =>
                {
                    if (x.Delta?.ToolCalls is not null) Console.Write(".");
                    else
                    {
                        Console.WriteWithColor(x?.Delta?.GetThinking(), ConsoleColor.DarkGray);
                        Console.WriteWithColor(x?.Delta?.Content, ConsoleColor.White);
                    }
                });
            }
            else Console.WriteLineWithColor(chunk.Message, ConsoleColor.Red);

        }
        void StreamOutputFinishedAction(Result result)
        {
            Console.WriteLine();
            Console.WriteLine("stop reason: " + result.FinishReason);
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
