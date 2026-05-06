using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Silmoon.AI.Models;
using Silmoon.AI.Models.OpenAI.Enums;
using Silmoon.AI.Models.OpenAI.Models;
using Silmoon.AI.OpenAI;
using Silmoon.Extensions;
using Silmoon.Intelligence.MauiClient.Models;
using Silmoon.Intelligence.MauiClient.Services;
using Silmoon.Models;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;


#if IOS
using Foundation;
using Microsoft.Maui.Platform;
using UIKit;
#endif

namespace Silmoon.Intelligence.MauiClient.Pages;

public partial class Chat : ContentPage
{
    public IntelligenceService intelligenceService;
    const double AutoScrollBottomThreshold = 24;
    bool shouldAutoScroll = true;
#if IOS
    NSObject? keyboardWillChangeFrameObserver;
    NSObject? keyboardWillHideObserver;
#endif

    ChatViewModel viewModel;
    public Chat()
    {
        intelligenceService = App.ServiceProvider.GetRequiredService<IntelligenceService>();
        InitializeComponent();
        BindingContext = viewModel = new ChatViewModel(this);
    }

    public Task ScrollHistoryToBottomAsync(bool animated = true, bool force = false)
    {
        if (!force && !shouldAutoScroll) return Task.CompletedTask;

        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            // Wait a tick so new content can be measured before scrolling.
            await Task.Delay(16);
            await HistoryScrollView.ScrollToAsync(0, double.MaxValue, animated);
        });
    }

    void HistoryScrollView_Scrolled(object? sender, ScrolledEventArgs e)
    {
        var maxScrollY = Math.Max(0, HistoryScrollView.ContentSize.Height - HistoryScrollView.Height);
        var distanceToBottom = maxScrollY - e.ScrollY;
        shouldAutoScroll = distanceToBottom <= AutoScrollBottomThreshold;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
#if IOS
        KeyboardAutoManagerScroll.Disconnect();
        RegisterKeyboardObservers();
#endif
    }

    protected override void OnDisappearing()
    {
#if IOS
        UnregisterKeyboardObservers();
        KeyboardAutoManagerScroll.Connect();
        RootLayout.Padding = new Thickness(0);
#endif
        base.OnDisappearing();
    }

#if IOS
    void RegisterKeyboardObservers()
    {
        if (keyboardWillChangeFrameObserver is not null) return;

        keyboardWillChangeFrameObserver = UIKeyboard.Notifications.ObserveWillChangeFrame((_, args) => OnKeyboardWillChangeFrame(args));
        keyboardWillHideObserver = UIKeyboard.Notifications.ObserveWillHide((_, args) => OnKeyboardWillHide(args));
    }

    void UnregisterKeyboardObservers()
    {
        keyboardWillChangeFrameObserver?.Dispose();
        keyboardWillChangeFrameObserver = null;
        keyboardWillHideObserver?.Dispose();
        keyboardWillHideObserver = null;
    }

    void OnKeyboardWillChangeFrame(UIKeyboardEventArgs args)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (Handler?.PlatformView is not UIView pageView || pageView.Window is null) return;
            if (InputBar.Handler?.PlatformView is not UIView inputBarView) return;

            // Convert frames to page-view coordinates, then compute actual overlap with input bar.
            var keyboardFrameInPage = pageView.ConvertRectFromView(args.FrameEnd, null);
            var inputBarFrameInPage = pageView.ConvertRectFromView(inputBarView.Bounds, inputBarView);
            var overlap = Math.Max(0, inputBarFrameInPage.Bottom - keyboardFrameInPage.Top);
            var requiredInset = Math.Max(0, overlap + 2); // keep a tiny visual gap
            RootLayout.Padding = new Thickness(0, 0, 0, requiredInset);
            await ScrollHistoryToBottomAsync(true, true);
            await Task.CompletedTask;
        });
    }

    void OnKeyboardWillHide(UIKeyboardEventArgs args)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            RootLayout.Padding = new Thickness(0);
            await Task.CompletedTask;
        });
    }
#endif
}
public partial class ChatViewModel : ObservableObject
{
    Chat page;
    [ObservableProperty]
    public partial ObservableCollection<ChatItem> Items { get; set; } = [];
    [ObservableProperty]
    public partial string Input { get; set; }

    public ChatViewModel(Chat page)
    {
        this.page = page;
        page.Title = $"Chat ({page.intelligenceService.AgentClient.NativeChatClient.ModelName})";
        page.intelligenceService.AgentClient.OnStreamOutput += NativeChatClient_OnStreamOutput;
        page.intelligenceService.AgentClient.OnStreamOutputCompleted += NativeChatClient_OnStreamOutputCompleted;

        page.intelligenceService.AgentClient.OnToolCallsStart += AgentClient_OnToolCallsStart; ;
        page.intelligenceService.AgentClient.OnToolExecuting += AgentClient_OnToolExecuting;
        page.intelligenceService.AgentClient.OnToolExecuted += AgentClient_OnToolExecuted;
        page.intelligenceService.AgentClient.OnToolCallsFinish += AgentClient_OnToolCallsFinish;
    }

    private Task AgentClient_OnToolCallsStart(ToolCallParameter[] toolCallParameters)
    {
        throw new NotImplementedException();
    }
    private Task AgentClient_OnToolExecuting(string functionName, ToolCallParameter toolCallParameter)
    {
        var lastChatItem = Items.LastOrDefault();
        ToolCallResult result = null;
        var parameters = toolCallParameter.Parameters;

        lastChatItem.Content += $"[TOOL CALL] {functionName}\r\n";
        switch (functionName)
        {
            case "Test_ToolCallTest":
                result = ToolCallResult.Create(toolCallParameter, true.ToStateSet<object>($"这是一个工具调用环境测试，正常！"));
                break;
            default:
                break;
        }
        return Task.CompletedTask;
    }
    private Task AgentClient_OnToolExecuted(string functionName, ToolCallParameter toolCallParameter, ToolCallResult toolCallResult)
    {
        var lastChatItem = Items.LastOrDefault();

        if (toolCallResult.Result.State) lastChatItem.Content += $"[TOOL RESULT] State: {toolCallResult.Result.State}, Message: {toolCallResult.Result.Message}\r\n";
        else lastChatItem.Content += $"[TOOL RESULT] State: {toolCallResult.Result.State}, Message: {toolCallResult.Result.Message}\r\n";
        return Task.CompletedTask;
    }
    private Task<ToolCallResult[]> AgentClient_OnToolCallsFinish(ToolCallParameter[] toolCallParameters, ToolCallResult[] toolCallResults)
    {
        return Task.FromResult(toolCallResults);
    }

    private Task NativeChatClient_OnStreamOutput(StateSet<bool, Chunk> chunkState)
    {
        if (chunkState.State)
        {
            chunkState.Data.Choices.Each(x =>
            {
                var lastChatItem = Items.LastOrDefault();
                if (x.Delta?.ToolCalls is not null)
                {
                    lastChatItem.Content += ".";
                }
                else
                {
                    Console.WriteWithColor(x?.Delta?.GetThinking(), ConsoleColor.DarkGray);
                    Console.WriteWithColor(x?.Delta?.Content, ConsoleColor.White);

                    lastChatItem.Content += x?.Delta?.GetThinking();
                    lastChatItem.Content += x?.Delta?.Content;
                }
            });
            _ = page.ScrollHistoryToBottomAsync(false);
        }
        return Task.CompletedTask;
    }
    private Task NativeChatClient_OnStreamOutputCompleted(Result result)
    {
        var lastChatItem = Items.LastOrDefault();

        lastChatItem.Content += $"\r\n[finish {result.FinishReason}]";
        if (result.FinishReason != "stop")
        {
            lastChatItem.Content += "\r\n";
        }
        _ = page.ScrollHistoryToBottomAsync();
        return Task.CompletedTask;
    }



    [RelayCommand]
    public async Task Chat()
    {
        if (string.IsNullOrWhiteSpace(Input))
        {
            await page.DisplayAlertAsync("输入不能为空", "请输入内容后再发送", "好的");
            Input = string.Empty;
        }
        else
        {
            Items.Add(new ChatItem { Role = Role.User, Content = Input });
            Items.Add(new ChatItem { Role = Role.Assistant, Content = string.Empty });
            _ = page.ScrollHistoryToBottomAsync(force: true);
            string input = Input;
            Input = string.Empty;
            var result = await page.intelligenceService.Input(input);
        }
    }
}