using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Silmoon.AI.Models;
using Silmoon.AI.Models.OpenAI.Enums;
using Silmoon.AI.Models.OpenAI.Models;
using Silmoon.Extensions;
using Silmoon.Intelligence.MauiClient.Services;
using Silmoon.Models;
using System.Collections.Concurrent;

namespace Silmoon.Intelligence.MauiClient.Pages;

public partial class Chat : ContentPage
{
    public IntelligenceService intelligenceService;

    ChatViewModel viewModel;
    public Chat()
    {
        intelligenceService = App.ServiceProvider.GetRequiredService<IntelligenceService>();
        BindingContext = viewModel = new ChatViewModel(this);
        InitializeComponent();
    }
}
public partial class ChatViewModel : ObservableObject
{
    Chat page;
    public ChatViewModel(Chat page)
    {
        this.page = page;
        page.intelligenceService.NativeChatClient.OnStreamOutput += NativeChatClient_OnStreamOutput;
        page.intelligenceService.NativeChatClient.OnStreamOutputCompleted += NativeChatClient_OnStreamOutputCompleted;
        page.intelligenceService.NativeChatClient.OnToolCallStart += NativeChatClient_OnToolCallStart;
        page.intelligenceService.NativeChatClient.OnToolCallCompleted += NativeChatClient_OnToolCallCompleted;
        page.Unloaded += (sender, e) =>
        {
            page.intelligenceService.NativeChatClient.OnStreamOutput -= NativeChatClient_OnStreamOutput;
            page.intelligenceService.NativeChatClient.OnStreamOutputCompleted -= NativeChatClient_OnStreamOutputCompleted;
        };
    }

    private async Task<ConcurrentDictionary<string, ToolCallResult>> NativeChatClient_OnToolCallCompleted(ConcurrentDictionary<string, ToolCallResult> toolCallResults)
    {
        foreach (var toolCallResult in toolCallResults.Values)
        {
            if (toolCallResult.Result.State) Output += $"[TOOL RESULT] State: {toolCallResult.Result.State}, Message: {toolCallResult.Result.Message}\r\n";
            else Output += $"[TOOL RESULT] State: {toolCallResult.Result.State}, Message: {toolCallResult.Result.Message}\r\n";
        }
        return await Task.FromResult(toolCallResults);
    }
    private async Task<List<ToolCallResult>> NativeChatClient_OnToolCallStart(ToolCallParameter[] toolCallParameters, ConcurrentDictionary<string, ToolCallResult> toolCallResults)
    {
        List<ToolCallResult> results = [];

        foreach (var parameter in toolCallParameters)
        {
            var functionName = parameter.FunctionName;
            var parameters = parameter.Parameters;

            Output += $"[TOOL CALL] {functionName}\r\n";
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
    private Task NativeChatClient_OnStreamOutputCompleted(Result result)
    {
        Output += $"\r\n[finish {result.FinishReason}]\r\n";
        return Task.CompletedTask;
    }
    private void NativeChatClient_OnStreamOutput(StateSet<bool, Chunk> chunkState)
    {
        if (chunkState.State)
        {
            chunkState.Data.Choices.Each(x =>
            {
                if (x.Delta?.ToolCalls is not null)
                {
                    Output += ".";
                }
                else
                {
                    Console.WriteWithColor(x?.Delta?.GetThinking(), ConsoleColor.DarkGray);
                    Console.WriteWithColor(x?.Delta?.Content, ConsoleColor.White);

                    Output += x?.Delta?.GetThinking();
                    Output += x?.Delta?.Content;
                }
            });
        }
    }


    [ObservableProperty]
    public partial string Output { get; set; }

    [ObservableProperty]
    public partial string Input { get; set; }

    [RelayCommand]
    public async Task Chat()
    {
        Output += $"User: {Input}\r\n";

        Output += "Assistant: ";
        string input = Input;
        Input = string.Empty;
        await page.intelligenceService.Input(input);
    }
}