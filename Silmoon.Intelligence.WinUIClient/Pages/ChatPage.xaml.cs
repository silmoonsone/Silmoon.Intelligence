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
    }
    public partial class ChatPageViewModel : ObservableObject
    {
        ChatPage Page;
        IntelligenceService IntelligenceService => Page.IntelligenceService;

        [ObservableProperty]
        public partial string ModelName { get; set; }
        [ObservableProperty]
        public partial ChatListItem SelectedChatListItem { get; set; }
        [ObservableProperty]
        public partial int SelectedChatListIndex { get; set; }
        [ObservableProperty]
        public partial ObservableCollection<ChatItem> Items { get; set; } = [];
        [ObservableProperty]
        public partial ObservableCollection<ChatListItem> ChatList { get; set; } = [];
        [ObservableProperty]
        public partial ObservableCollection<ToolExecuteIndicatorModel> ToolExecuteIndicators { get; set; } = [];
        [ObservableProperty]
        public partial string UserInput { get; set; }
        [ObservableProperty]
        public partial bool UserInputAvaliable { get; set; } = true;
        [ObservableProperty]
        public partial KeyValuePair<Guid, AgentClient> CurrentAgentClient { get; set; }
        [ObservableProperty]
        public partial string UsageInfo { get; set; }
        public ChatPageViewModel(ChatPage page)
        {
            Page = page;
            PropertyChanged += ChatPageViewModel_PropertyChanged;

            IntelligenceService.ReadyResetEvent.WaitOne();
            if (!IntelligenceService.AgentClients.IsNullOrEmpty()) LoadChatList();
            SelectedChatListItem = ChatList.FirstOrDefault();
        }

        private async void ChatPageViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectedChatListItem))
            {
                if (SelectedChatListItem is not null)
                    await SwitchChat(SelectedChatListItem.Id);
                else if (ChatList.Count != 0)
                    SelectedChatListIndex = 0;
            }
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
                ChatList.Insert(0, new ChatListItem() { Id = id, ChatCounting = agent.History.Count, CreatedAt = agent.State.CreatedAt, LastAt = agent.State.LastAt, Topic = agent.Topic.IsNullOrEmpty() ? $"#{id}" : agent.Topic, This = this });
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
                            if (!message.Content.IsNullOrDefault()) lastChatItem.Content += $"\r\n{IntelligenceService.GetUserRealInput(message.Content)}";
                            if (!message.ReasoningContent.IsNullOrDefault()) lastChatItem.ReasoningContent += $"\r\n{IntelligenceService.GetUserRealInput(message.ReasoningContent)}";
                            message.ToolCalls?.Each(toolcall =>
                            {
                                if (lastChatItem.Content.EndsWith("\r\n")) lastChatItem.Content += $"\r\n*[工具调用：{toolcall.Function.Name}]*\r\n";
                                else lastChatItem.Content += $"\r\n\r\n*[工具调用：{toolcall.Function.Name}]*\r\n";
                            });
                            lastChatItem.ContentVisual = true;
                            lastChatItem.ReasoningContentVisual = true;
                        }
                        else
                        {
                            var content = IntelligenceService.GetUserRealInput(message.Content);
                            var reasoningContent = IntelligenceService.GetUserRealInput(message.ReasoningContent);
                            message.ToolCalls?.Each(toolcall =>
                            {
                                if (content?.EndsWith("\r\n") ?? false) content += $"\r\n*[工具调用：{toolcall.Function.Name}]*\r\n";
                                else content += $"\r\n\r\n*[工具调用：{toolcall.Function.Name}]*\r\n";
                            });
                            Items.Add(new ChatItem(message.Role, content, reasoningContent));
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
                            if (indicator is null) ToolExecuteIndicators.Add(new ToolExecuteIndicatorModel { FunctionName = "工具调用", Status = "生成中...", Color = new SolidColorBrush(Colors.YellowGreen), ToolCallId = string.Empty });

                            if (!lastChatItem.StreamingReasoningContent.IsNullOrEmpty() && !lastChatItem.StreamingReasoningContent.EndsWith("\r\n")) lastChatItem.StreamingReasoningContent += "\r\n";
                            if (!lastChatItem.StreamingContent.IsNullOrEmpty() && !lastChatItem.StreamingContent.EndsWith("\r\n")) lastChatItem.StreamingContent += "\r\n";
                        }
                        else
                        {
                            var indicator = ToolExecuteIndicators.FirstOrDefault(x => x.ToolCallId == string.Empty);
                            indicator?.Status = "完成";

                            //Console.WriteWithColor(x?.Delta?.GetThinking(), ConsoleColor.DarkGray);
                            //Console.WriteWithColor(x?.Delta?.Content, ConsoleColor.White);

                            var thinkingContent = x?.Delta?.GetThinking();
                            var content = x?.Delta?.Content;

                            if (!thinkingContent.IsNullOrEmpty())
                            {
                                if (!lastChatItem.StreamingContent.IsNullOrEmpty() && !lastChatItem.StreamingContent.EndsWith("\r\n")) lastChatItem.StreamingContent += "\r\n";
                                lastChatItem.StreamingReasoningContent += thinkingContent;
                            }

                            if (!content.IsNullOrEmpty())
                            {
                                if (!lastChatItem.StreamingReasoningContent.IsNullOrEmpty() && !lastChatItem.StreamingReasoningContent.EndsWith("\r\n")) lastChatItem.StreamingReasoningContent += "\r\n";
                                lastChatItem.StreamingContent += content;
                            }
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
                lastChatItem.StreamingContent += $"\r\n\r\n*[finish {result.FinishReason}]*";

                if (result.FinishReason == "stop")
                {
                    lastChatItem.Content = lastChatItem.StreamingContent.TrimEnd("\r\n").ToString();
                    lastChatItem.ReasoningContent = lastChatItem.StreamingReasoningContent.TrimEnd("\r\n").ToString();

                    lastChatItem.StreamingContent = string.Empty;
                    lastChatItem.StreamingReasoningContent = string.Empty;

                    lastChatItem.ContentVisual = true;
                    lastChatItem.ReasoningContentVisual = true;

                    lastChatItem.StreamingContentVisual = false;
                    lastChatItem.StreamingReasoningContentVisual = false;
                }
                else if (result.FinishReason != "stop")
                {
                    lastChatItem.StreamingContent += "\r\n\r\n";
                }
                _ = Page.ScrollHistoryToBottomAsync();
                UsageInfo = $"total tokens: {result.Usage.TotalTokens:N0}, prompt tokens: {result.Usage.PromptTokens:N0}, completion tokens: {result.Usage.CompletionTokens:N0}";
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
            ChatList.Insert(0, new ChatListItem() { Id = newChat.Data.Key, ChatCounting = newChat.Data.Value.History.Count, CreatedAt = DateTime.Now, LastAt = DateTime.Now, Topic = $"#{newChat.Data.Key}", This = this });
            var history = new List<IMessage>(IntelligenceService.AgentClients[chatListItem.Id].History);
            newChat.Data.Value.NativeChatClient.MessageHistory = history;
            Page.nameChatList.SelectedIndex = 0;
        }
        [RelayCommand]
        public async Task GenerateTopic(ChatListItem chatListItem)
        {
            var result = await IntelligenceService.GenerateAgentTopic(chatListItem.Id);
            chatListItem.Topic = result;
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
                Items.Add(new ChatItem(Role.Assistant, string.Empty) { StreamingContentVisual = true, StreamingReasoningContentVisual = true });
                await Page.ScrollHistoryToBottomAsync(force: true);
                string input = UserInput;
                UserInput = string.Empty;
                UsageInfo = string.Empty;
                var result = await IntelligenceService.Chat(input, CurrentAgentClient.Key, true);
                if (SelectedChatListItem.Topic.StartsWith('#') && !CurrentAgentClient.Value.Topic.IsNullOrEmpty()) SelectedChatListItem.Topic = CurrentAgentClient.Value.Topic;
            }
        }
        [RelayCommand]
        public void SelectImages()
        {

        }


        [RelayCommand]
        public void SaveHistory()
        {
            IntelligenceService.SaveChatState(CurrentAgentClient.Key);
        }
        [RelayCommand]
        public void ClearHistory()
        {
            CurrentAgentClient.Value.NativeChatClient.ClearHistory();
            IntelligenceService.SaveChatState(CurrentAgentClient.Key);
            Page.DispatcherQueue.TryEnqueue(Items.Clear);
        }
        [RelayCommand]
        public async Task UndoHistory()
        {
            CurrentAgentClient.Value.RollbackHistory();
            Items.Clear();
            await LoadChat();

            //IntelligenceService.SaveChatHistory();
            //Page.DispatcherQueue.TryEnqueue(Items.Clear);
        }
        [RelayCommand]
        public async Task<StateSet<bool, KeyValuePair<Guid, AgentClient>>> NewChat()
        {
            var newChat = IntelligenceService.NewAgent();
            ChatList.Insert(0, new ChatListItem() { Id = newChat.Data.Key, ChatCounting = newChat.Data.Value.History.Count, CreatedAt = DateTime.Now, LastAt = DateTime.Now, Topic = $"#{newChat.Data.Key}", This = this });
            Page.nameChatList.SelectedIndex = 0;
            return newChat;
        }
        [RelayCommand]
        public async Task SwitchChat(Guid chatId)
        {
            UsageInfo = null;
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