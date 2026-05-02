using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using Silmoon.AI.Models;
using Silmoon.AI.Models.OpenAI.Enums;
using Silmoon.AI.Models.OpenAI.Models;
using Silmoon.AI.OpenAI;
using Silmoon.AI.Prompts;
using Silmoon.Extensions;
using Silmoon.Extensions.Hosting.Interfaces;
using System;
using System.Collections.Concurrent;

namespace Silmoon.Intelligence.Hosting.Services;

public class ClientService : IHostedService
{
    NativeChatClient NativeChatClient { get; set; }
    SilmoonConfigureServiceImpl SilmoonConfigureService { get; set; }
    IHostApplicationLifetime ApplicationLifetime { get; set; }
    ContextManagerService ContextManagerService { get; set; }
    public ClientService(ISilmoonConfigureService silmoonConfigureService, ContextManagerService contextManagerService, IHostApplicationLifetime applicationLifetime)
    {
        ApplicationLifetime = applicationLifetime;
        ContextManagerService = contextManagerService;
        ApplicationLifetime.ApplicationStarted.Register(async () => await Start());
        SilmoonConfigureService = silmoonConfigureService as SilmoonConfigureServiceImpl;

        NativeChatClient = new NativeChatClient(SilmoonConfigureService.DefaultProvider, SilmoonConfigureService.DefaultModelName, UtilPrompt.ContextPrompt);
        NativeChatClient.OnToolCallStart += NativeChatClient_OnToolCallStart;
        NativeChatClient.OnToolCallCompleted += NativeChatClient_OnToolCallCompleted;
        ContextManagerService.InjectTools(NativeChatClient);
        NativeChatClient.Tools.Add(Tool.Create("ToolCallTestTool", "This is a test tool_calling test tool.", []));
        //NativeChatClient.EnableThinking = true;
    }

    private Task<ToolCallResult> NativeChatClient_OnToolCallCompleted(ToolCallResult toolCallResult)
    {
        if (toolCallResult.Result.State) Console.WriteLineWithColor($"[TOOL RESULT] State: {toolCallResult.Result.State}, Message: {toolCallResult.Result.Message}", ConsoleColor.Cyan);
        else Console.WriteLineWithColor($"[TOOL RESULT] State: {toolCallResult.Result.State}, Message: {toolCallResult.Result.Message}", ConsoleColor.Red);
        return Task.FromResult(toolCallResult);
    }
    private async Task<ToolCallResult> NativeChatClient_OnToolCallStart(ToolCallParameter toolCallParameter, ToolCallResult toolCallResult)
    {
        if (toolCallParameter.FunctionName == "ToolCallTestTool")
            return await Task.FromResult(ToolCallResult.Create(toolCallParameter, false.ToStateSet<string>("这是一个工具调用环境测试，正常！")));
        else return null;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task Start(bool stream = true)
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
                            NativeChatClient.ResetHistory();
                            Console.WriteLine("Message history cleared.");
                            break;
                        case "exit":
                            Console.WriteLine("Exiting application...");
                            ApplicationLifetime.StopApplication();
                            return;
                        default:
                            Console.WriteLine($"Unknown command: {command}");
                            break;
                    }
                }
                else
                {
                    Console.Write(Role.Assistant + ": ");

                    if (stream)
                    {
                        List<Chunk> chunks = [];
                        await foreach (var chunk in NativeChatClient.CompletionsStreamAsync(input, chunks))
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
                        Console.WriteLine();
                        var result = Result.Create([.. chunks]);
                        Console.WriteLine($"【完成{result.FinishReason}】");
                        if (result.FinishReason == "tool_calls") Console.WriteWithColor(result.ToolCalls.ToFormattedJsonString(), ConsoleColor.DarkYellow);
                    }
                    else
                    {
                        Response response = await NativeChatClient.CompletionsAsync(input);
                        response.Choices.Each(x => Console.WriteWithColor(x?.Message?.Content, ConsoleColor.White));
                        Console.WriteLine($"【完成{response.Choices[0].FinishReason}】");
                        if (response.Choices[0].FinishReason == "tool_calls") Console.WriteWithColor(response.Choices[0].Message.ToolCalls?.ToFormattedJsonString(), ConsoleColor.DarkYellow);
                    }
                    Console.WriteLine();
                }
            }
        });
    }
}
