using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
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

        ChatPageViewModel ViewModel;
        ScrollViewer? chatScrollViewer;
        bool shouldAutoScroll = true;

        public IntelligenceService IntelligenceService { get; set; }
        public ChatPage()
        {
            IntelligenceService = App.GetService<IntelligenceService>();
            InitializeComponent();
            Loaded += ChatPage_Loaded;
            Unloaded += ChatPage_Unloaded;
            ViewModel = new ChatPageViewModel(this);
        }

        void ChatPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (chatScrollViewer is not null) return;
            chatScrollViewer = FindDescendantScrollViewer(nameChat);
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
                    ViewModel.UserInput += "\r\n";
                    nameUserInput.SelectionStart = ViewModel.UserInput.Length;
                }
                else ViewModel.SendChatCommand.Execute(null);
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

        private async void nameChatList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is ChatListItem newChat)
            {
                await ViewModel.SwitchChat(newChat.Id);
            }
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
        public partial ObservableCollection<ChatListItem> ChatList { get; set; } = [];
        [ObservableProperty]
        public partial ObservableCollection<ToolExecuteIndicatorModel> ToolExecuteIndicators { get; set; } = [];
        [ObservableProperty]
        public partial string UserInput { get; set; }
        [ObservableProperty]
        public partial bool UserInputAvaliable { get; set; } = false;
        [ObservableProperty]
        public partial KeyValuePair<Guid, AgentClient> CurrentAgentClient { get; set; }
        public ChatPageViewModel(ChatPage page)
        {
            Page = page;
            IntelligenceService.ReadyResetEvent.WaitOne();

            if (!IntelligenceService.AgentClients.IsNullOrEmpty())
            {
                LoadChatList();
                var pkvClient = IntelligenceService.AgentClients.LastOrDefault();
                if (pkvClient.Value is not null)
                    page.nameChatList.SelectedIndex = 0;
            }
            UserInputAvaliable = true;
        }

        public void BindEvents()
        {
            CurrentAgentClient.Value?.OnStreamOutput += MainChatAgentClient_OnStreamOutput;
            CurrentAgentClient.Value?.OnStreamOutputCompleted += MainChatAgentClient_OnStreamOutputCompleted;
            CurrentAgentClient.Value?.OnToolCallsStart += MainChatAgentClient_OnToolCallsStart; ;
            CurrentAgentClient.Value?.OnToolExecuting += MainChatAgentClient_OnToolExecuting;
            CurrentAgentClient.Value?.OnToolExecuted += MainChatAgentClient_OnToolExecuted;
            CurrentAgentClient.Value?.OnToolCallsFinish += MainChatAgentClient_OnToolCallsFinish;
        }
        public void UnbindEvents()
        {
            CurrentAgentClient.Value?.OnStreamOutput -= MainChatAgentClient_OnStreamOutput;
            CurrentAgentClient.Value?.OnStreamOutputCompleted -= MainChatAgentClient_OnStreamOutputCompleted;
            CurrentAgentClient.Value?.OnToolCallsStart -= MainChatAgentClient_OnToolCallsStart; ;
            CurrentAgentClient.Value?.OnToolExecuting -= MainChatAgentClient_OnToolExecuting;
            CurrentAgentClient.Value?.OnToolExecuted -= MainChatAgentClient_OnToolExecuted;
            CurrentAgentClient.Value?.OnToolCallsFinish -= MainChatAgentClient_OnToolCallsFinish;
        }
        public ObservableCollection<ChatListItem> LoadChatList()
        {
            ChatList.Clear();
            foreach (var kvp in IntelligenceService.AgentClients)
            {
                var agent = kvp.Value;
                var id = kvp.Key;
                ChatList.Insert(0, new ChatListItem() { Id = id, ChatCounting = agent.History.Count, CreatedAt = DateTime.Now, LatestAt = DateTime.Now, DisplayName = $"{id}", This = this });
            }
            return ChatList;
        }
        public async Task LoadChat()
        {
            Items.Clear();
            if (CurrentAgentClient.Value is null) return;
            foreach (var item in CurrentAgentClient.Value.History)
            {
                if (item is MessageContent message)
                {
                    if (item.Role == Role.Assistant || item.Role == Role.User)
                    {
                        //相同角色合并
                        var lastChatItem = Items.LastOrDefault();
                        if (lastChatItem is not null && (lastChatItem.Role == item.Role))
                        {
                            if (!message.Content.IsNullOrDefault()) lastChatItem.FinishContent += $"\r\n{IntelligenceService.GetUserRealInput(message.Content)}";
                            message.ToolCalls?.Each(toolcall =>
                            {
                                if (lastChatItem.FinishContent.EndsWith("\r\n")) lastChatItem.FinishContent += $"\r\n*[工具调用：{toolcall.Function.Name}]*\r\n";
                                else lastChatItem.FinishContent += $"\r\n\r\n*[工具调用：{toolcall.Function.Name}]*\r\n";
                            });
                            lastChatItem.FinishContentVisual = true;
                        }
                        else
                        {
                            var content = IntelligenceService.GetUserRealInput(message.Content);
                            message.ToolCalls?.Each(toolcall =>
                            {
                                if (content?.EndsWith("\r\n") ?? false) content += $"\r\n*[工具调用：{toolcall.Function.Name}]*\r\n";
                                else content += $"\r\n\r\n*[工具调用：{toolcall.Function.Name}]*\r\n";
                            });
                            Items.Add(new ChatItem(message.Role, content));
                        }
                    }
                }
            }
            await Task.Delay(100);
            await Page.ScrollHistoryToBottomAsync(force: true);
            await Task.Delay(500);
            await Page.ScrollHistoryToBottomAsync(force: true);
        }

        private async Task MainChatAgentClient_OnToolCallsStart(ToolCallParameter[] toolCallParameters)
        {
            //throw new NotImplementedException();
        }
        private Task MainChatAgentClient_OnToolExecuting(string functionName, ToolCallParameter toolCallParameter)
        {
            Page.DispatcherQueue.TryEnqueue(() =>
            {
                ToolExecuteIndicators.Add(new ToolExecuteIndicatorModel { FunctionName = functionName, Color = new SolidColorBrush(Colors.Orange), Status = "执行中", ToolCallId = toolCallParameter.ToolCallId });
                if (Items.LastOrDefault() is null) return;
                _ = Page.ScrollHistoryToBottomAsync(animated: false, force: false);
            });
            return Task.CompletedTask;
        }
        private Task MainChatAgentClient_OnToolExecuted(string functionName, ToolCallParameter toolCallParameter, ToolCallResult toolCallResult)
        {
            Page.DispatcherQueue.TryEnqueue(() =>
            {
                var indicator = ToolExecuteIndicators.FirstOrDefault(x => x.ToolCallId == toolCallParameter.ToolCallId);
                if (indicator is not null)
                {
                    if (toolCallResult.Result.State)
                    {
                        indicator.Color = new SolidColorBrush(Colors.Green);
                        indicator.Status = "完成";
                    }
                    else
                    {
                        indicator.Color = new SolidColorBrush(Colors.Red);
                        indicator.Status = "错误";
                    }
                }
                var lastChatItem = Items.LastOrDefault();

                if (lastChatItem is not null)
                {
                    //if (toolCallResult.Result.State) lastChatItem.Content += $"[TOOL RESULT] State: {toolCallResult.Result.State}, Message: {toolCallResult.Result.Message}\r\n";
                    //else lastChatItem.Content += $"[TOOL RESULT] State: {toolCallResult.Result.State}, Message: {toolCallResult.Result.Message}\r\n";
                    _ = Page.ScrollHistoryToBottomAsync(animated: false, force: false);
                }
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
                            var indicator = ToolExecuteIndicators.FirstOrDefault(x => x.ToolCallId == string.Empty);
                            if (indicator is null) ToolExecuteIndicators.Add(new ToolExecuteIndicatorModel { FunctionName = "工具调用", Status = "等待...", Color = new SolidColorBrush(Colors.YellowGreen), ToolCallId = string.Empty });
                            //lastChatItem.Content += ".";
                        }
                        else
                        {
                            var indicator = ToolExecuteIndicators.FirstOrDefault(x => x.ToolCallId == string.Empty);
                            indicator?.Status = "完成";

                            Console.WriteWithColor(x?.Delta?.GetThinking(), ConsoleColor.DarkGray);
                            Console.WriteWithColor(x?.Delta?.Content, ConsoleColor.White);

                            lastChatItem.StreamContent += x?.Delta?.GetThinking();
                            lastChatItem.StreamContent += x?.Delta?.Content;
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
                ToolExecuteIndicators.Clear();
                var lastChatItem = Items.LastOrDefault();
                if (lastChatItem is null) return;
                lastChatItem.StreamContent += $"\r\n\r\n*[finish {result.FinishReason}]*";

                if (result.FinishReason == "stop")
                {
                    lastChatItem.FinishContent = lastChatItem.StreamContent;
                    lastChatItem.StreamContent = result.ReasoningContent;
                    lastChatItem.StreamContentVisual = !lastChatItem.StreamContent.IsNullOrEmpty();
                    lastChatItem.FinishContentVisual = !lastChatItem.FinishContent.IsNullOrEmpty();
                }
                else if (result.FinishReason != "stop")
                {
                    lastChatItem.StreamContent += "\r\n\r\n";
                }
                _ = Page.ScrollHistoryToBottomAsync();
            });
            return Task.CompletedTask;
        }

        [RelayCommand]
        public void DeleteChat(ChatListItem chatListItem)
        {
            IntelligenceService.DeleteAgent(chatListItem.Id);
            ChatList.Remove(chatListItem);
            if (chatListItem.Id == CurrentAgentClient.Key)
                Page.nameChatList.SelectedIndex = 0;
        }
        [RelayCommand]
        public async Task CopyChat(ChatListItem chatListItem)
        {
            var newChat = IntelligenceService.NewAgent();
            ChatList.Insert(0, new ChatListItem() { Id = newChat.Data.Key, ChatCounting = newChat.Data.Value.History.Count, CreatedAt = DateTime.Now, LatestAt = DateTime.Now, DisplayName = $"{newChat.Data.Key}", This = this });
            var history = new List<IMessage>(IntelligenceService.AgentClients[chatListItem.Id].History);
            newChat.Data.Value.NativeChatClient.MessageHistory = history;
            Page.nameChatList.SelectedIndex = 0;
        }

        [RelayCommand]
        public async Task SendChat()
        {
            if (string.IsNullOrWhiteSpace(UserInput))
            {
                //await Page.DisplayAlertAsync("输入不能为空", "请输入内容后再发送", "好的");
                UserInput = string.Empty;
            }
            else
            {
                Items.Add(new ChatItem(Role.User, UserInput));
                Items.Add(new ChatItem(Role.Assistant, string.Empty) { StreamContentVisual = true });
                await Page.ScrollHistoryToBottomAsync(force: true);
                string input = UserInput;
                UserInput = string.Empty;
                var result = await IntelligenceService.Chat(input, CurrentAgentClient.Key, true);
            }
        }
        [RelayCommand]
        public void SelectImages()
        {

        }


        [RelayCommand]
        public void SaveHistory()
        {
            IntelligenceService.SaveChatHistory(CurrentAgentClient.Key);
        }
        [RelayCommand]
        public void ClearHistory()
        {
            CurrentAgentClient.Value.NativeChatClient.ResetHistory();
            IntelligenceService.SaveChatHistory(CurrentAgentClient.Key);
            Page.DispatcherQueue.TryEnqueue(Items.Clear);
        }
        [RelayCommand]
        public async Task UndoHistory()
        {
            CurrentAgentClient.Value.NativeChatClient.RollbackHistory();
            Items.Clear();
            await LoadChat();

            //IntelligenceService.SaveChatHistory();
            //Page.DispatcherQueue.TryEnqueue(Items.Clear);
        }
        [RelayCommand]
        public async Task<StateSet<bool, KeyValuePair<Guid, AgentClient>>> NewChat()
        {
            var newChat = IntelligenceService.NewAgent();
            ChatList.Insert(0, new ChatListItem() { Id = newChat.Data.Key, ChatCounting = newChat.Data.Value.History.Count, CreatedAt = DateTime.Now, LatestAt = DateTime.Now, DisplayName = $"{newChat.Data.Key}", This = this });
            Page.nameChatList.SelectedIndex = 0;
            return newChat;
        }
        [RelayCommand]
        public async Task SwitchChat(Guid chatId)
        {
            UnbindEvents();
            var isFind = IntelligenceService.AgentClients.TryGetValue(chatId, out var agentClient);
            if (isFind)
            {
                CurrentAgentClient = new KeyValuePair<Guid, AgentClient>(chatId, agentClient);
                ModelName = agentClient.NativeChatClient.ModelName;
                BindEvents();
                await LoadChat();
            }
        }
    }
}