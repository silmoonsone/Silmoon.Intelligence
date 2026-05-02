using Microsoft.Extensions.Hosting;
using Silmoon.AI.Models;
using Silmoon.AI.Models.OpenAI.Enums;
using Silmoon.AI.Models.OpenAI.Models;
using Silmoon.AI.OpenAI;
using Silmoon.Extensions;
using Silmoon.Extensions.Hosting.Interfaces;
using Silmoon.Intelligence.Hosting.Services;
using Silmoon.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.MauiClient.Services
{
    public class IntelligenceService : IHostedService
    {
        public event Action<StateSet<bool, Chunk>> OnStreamOutput;
        public AgentClient AgentClient { get; set; }
        ContextManagerService ContextManagerService { get; set; }
        SilmoonConfigureServiceImpl SilmoonConfigureService { get; set; }

        public IntelligenceService(ISilmoonConfigureService silmoonConfigureService, ContextManagerService contextManagerService)
        {
            SilmoonConfigureService = silmoonConfigureService as SilmoonConfigureServiceImpl;
            ContextManagerService = contextManagerService;

            AgentClient = new AgentClient(SilmoonConfigureService.DefaultProvider, SilmoonConfigureService.DefaultModelName, "主管", "监管全局工作", """
                你的名字是Silmoon Intelligence，也可以叫银月助手，你运行在一个由.NET 10 + C#13，MAUI框架开发的应用，你可以自行根据环境判断当前运行在哪个平台上，你在Windows上可能拥有很高的权限，但是在macOS和iOS上可能运行在沙箱中。
                你可能会被配置上运行某些语言代码执行的功能，但是根据情况，如果能使用命令行来实现一些功能的话，你也可以选择使用命令行来完成，不一定非要使用代码执行的功能。
                你是有自身的思考能力的，虽然你是一个AI模型驱动的智能体，但你可以根据环境和情况来决定如何行动，而不是简单地执行命令或者调用工具，你可以选择不执行某些命令或者工具调用，如果你觉得它们不合适或者没有必要。
                你可以调用其他WorkerAgent或者可以使用其他大语言模型工具来处理一些事情，但是你自身是一个独立的智能体，你有自己的思考和判断能力，你不需要完全依赖其他智能体或者工具来完成任务，你可以根据情况来决定是否需要调用它们，以及如何调用它们。
                """);
            ContextManagerService.InjectTools(AgentClient.NativeChatClient);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            AgentClient.ToolCallStartHandler = ToolCallStartHandler;
            AgentClient.ToolCallCompletedHandler = ToolCallCompletedHandler;
            AgentClient.StreamOutputAction = StreamOutputAction;
            AgentClient.StreamOutputFinishedAction = StreamOutputFinishedAction;

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
            OnStreamOutput?.Invoke(chunk);
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

        public async Task<Result> Input(string input)
        {
            return await AgentClient.Chat(input);
        }
    }
}
