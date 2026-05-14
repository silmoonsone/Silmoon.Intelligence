using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Silmoon.AI.Models;
using Silmoon.AI.Models.OpenAI.Enums;
using Silmoon.AI.Models.OpenAI.Models;
using Silmoon.Extensions;
using Silmoon.Intelligence.Hosting.Services;
using Silmoon.Intelligence.WinUIClient.Models;
using Silmoon.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Core;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Silmoon.Intelligence.WinUIClient.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ChatPage : Page
    {
        const double AutoScrollBottomThreshold = 24;

        ChatPageViewModel viewModel;
        ScrollViewer? chatScrollViewer;
        bool shouldAutoScroll = true;

        public IntelligenceService IntelligenceService { get; set; }
        public ChatPage()
        {
            IntelligenceService = App.GetService<IntelligenceService>();
            DataContext = viewModel = new ChatPageViewModel(this);
            InitializeComponent();
            Loaded += ChatPage_Loaded;
            Unloaded += ChatPage_Unloaded;
        }

        void ChatPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (chatScrollViewer is not null) return;
            chatScrollViewer = FindDescendantScrollViewer(nameChatList);
            if (chatScrollViewer is not null)
                chatScrollViewer.ViewChanged += ChatScrollViewer_ViewChanged;
        }

        void ChatPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (chatScrollViewer is not null)
            {
                chatScrollViewer.ViewChanged -= ChatScrollViewer_ViewChanged;
                chatScrollViewer = null;
            }
        }

        void ChatScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            if (sender is not ScrollViewer sv) return;
            var scrollable = sv.ScrollableHeight;
            if (double.IsNaN(scrollable) || double.IsInfinity(scrollable)) return;
            var distanceToBottom = scrollable - sv.VerticalOffset;
            shouldAutoScroll = distanceToBottom <= AutoScrollBottomThreshold;
        }

        /// <summary>
        /// 将聊天记录滚到底部。流式输出用 <paramref name="animated"/>=false、<paramref name="force"/>=false；
        /// 用户发送后用 <paramref name="force"/>=true。
        /// </summary>
        public Task ScrollHistoryToBottomAsync(bool animated = true, bool force = false)
        {
            if (!force && !shouldAutoScroll) return Task.CompletedTask;

            if (DispatcherQueue.HasThreadAccess)
                return RunScrollCoreAsync(animated);

            var tcs = new TaskCompletionSource();
            if (!DispatcherQueue.TryEnqueue(() => _ = RunScrollWithCompletionAsync(tcs, animated)))
                tcs.TrySetException(new InvalidOperationException("DispatcherQueue.TryEnqueue failed."));
            return tcs.Task;
        }

        async Task RunScrollWithCompletionAsync(TaskCompletionSource tcs, bool animated)
        {
            try
            {
                await RunScrollCoreAsync(animated);
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }

        async Task RunScrollCoreAsync(bool animated)
        {
            await Task.Delay(16);
            if (chatScrollViewer is null)
                chatScrollViewer = FindDescendantScrollViewer(nameChatList);
            if (chatScrollViewer is null) return;
            var y = chatScrollViewer.ScrollableHeight;
            if (double.IsNaN(y) || double.IsInfinity(y)) return;
            chatScrollViewer.ChangeView(null, y, null, disableAnimation: !animated);
        }

        void nameUserInput_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
                if (shift.HasFlag(CoreVirtualKeyStates.Down))
                {
                    viewModel.UserInput += Environment.NewLine;
                    nameUserInput.SelectionStart = viewModel.UserInput.Length;
                }
                else viewModel.SendMessageCommand.Execute(null);
                e.Handled = true;
            }
        }

        static ScrollViewer? FindDescendantScrollViewer(DependencyObject? root)
        {
            if (root is null) return null;
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is ScrollViewer sv) return sv;
                var nested = FindDescendantScrollViewer(child);
                if (nested is not null) return nested;
            }
            return null;
        }
    }
    public partial class ChatPageViewModel : ObservableObject
    {
        ChatPage Page;
        IntelligenceService IntelligenceService => Page.IntelligenceService;

        [ObservableProperty]
        public partial string ModelName { get; set; }
        [ObservableProperty]
        public partial ObservableCollection<ChatItem> Items { get; set; } = [];
        [ObservableProperty]
        public partial string UserInput { get; set; }
        public ChatPageViewModel(ChatPage page)
        {
            Page = page;
            ModelName = page.IntelligenceService.MainChatAgentClient.NativeChatClient.ModelName;

            page.IntelligenceService.MainChatAgentClient.OnStreamOutput += MainChatAgentClient_OnStreamOutput;
            page.IntelligenceService.MainChatAgentClient.OnStreamOutputCompleted += MainChatAgentClient_OnStreamOutputCompleted;

            page.IntelligenceService.MainChatAgentClient.OnToolCallsStart += MainChatAgentClient_OnToolCallsStart; ;
            page.IntelligenceService.MainChatAgentClient.OnToolExecuting += MainChatAgentClient_OnToolExecuting;
            page.IntelligenceService.MainChatAgentClient.OnToolExecuted += MainChatAgentClient_OnToolExecuted;
            page.IntelligenceService.MainChatAgentClient.OnToolCallsFinish += MainChatAgentClient_OnToolCallsFinish;
        }

        private async Task MainChatAgentClient_OnToolCallsStart(ToolCallParameter[] toolCallParameters)
        {
            //throw new NotImplementedException();
        }
        private Task MainChatAgentClient_OnToolExecuting(string functionName, ToolCallParameter toolCallParameter)
        {
            Page.DispatcherQueue.TryEnqueue(() =>
            {
                var lastChatItem = Items.LastOrDefault();
                if (lastChatItem is null) return;
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
                _ = Page.ScrollHistoryToBottomAsync(animated: false, force: false);
            });
            return Task.CompletedTask;
        }
        private Task MainChatAgentClient_OnToolExecuted(string functionName, ToolCallParameter toolCallParameter, ToolCallResult toolCallResult)
        {
            Page.DispatcherQueue.TryEnqueue(() =>
            {
                var lastChatItem = Items.LastOrDefault();
                if (lastChatItem is null) return;

                if (toolCallResult.Result.State) lastChatItem.Content += $"[TOOL RESULT] State: {toolCallResult.Result.State}, Message: {toolCallResult.Result.Message}\r\n";
                else lastChatItem.Content += $"[TOOL RESULT] State: {toolCallResult.Result.State}, Message: {toolCallResult.Result.Message}\r\n";
                _ = Page.ScrollHistoryToBottomAsync(animated: false, force: false);
            });
            return Task.CompletedTask;
        }
        private Task<ToolCallResult[]> MainChatAgentClient_OnToolCallsFinish(ToolCallParameter[] toolCallParameters, ToolCallResult[] toolCallResults)
        {
            return Task.FromResult(toolCallResults);
        }

        private Task MainChatAgentClient_OnStreamOutput(StateSet<bool, Chunk> chunkState)
        {
            if (chunkState.State)
            {
                chunkState.Data.Choices.Each(x =>
                {
                    Page.DispatcherQueue.TryEnqueue(() =>
                    {
                        var lastChatItem = Items.LastOrDefault();
                        if (lastChatItem is null) return;
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
                });
                Page.DispatcherQueue.TryEnqueue(() => _ = Page.ScrollHistoryToBottomAsync(animated: false, force: false));
            }
            return Task.CompletedTask;
        }
        private Task MainChatAgentClient_OnStreamOutputCompleted(Result result)
        {
            Page.DispatcherQueue.TryEnqueue(() =>
            {
                var lastChatItem = Items.LastOrDefault();
                if (lastChatItem is null) return;
                lastChatItem.Content += $"\r\n[finish {result.FinishReason}]";
                if (result.FinishReason != "stop")
                {
                    lastChatItem.Content += "\r\n";
                }
                _ = Page.ScrollHistoryToBottomAsync();
            });
            return Task.CompletedTask;
        }


        [RelayCommand]
        public async Task SendMessage()
        {
            if (string.IsNullOrWhiteSpace(UserInput))
            {
                //await Page.DisplayAlertAsync("输入不能为空", "请输入内容后再发送", "好的");
                UserInput = string.Empty;
            }
            else
            {
                Items.Add(new ChatItem { Role = Role.User, Content = UserInput });
                Items.Add(new ChatItem { Role = Role.Assistant, Content = string.Empty });
                await Page.ScrollHistoryToBottomAsync(force: true);
                string input = UserInput;
                UserInput = string.Empty;
                var result = await IntelligenceService.Input(input);
            }
        }
    }
}
