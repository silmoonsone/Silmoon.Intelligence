using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Silmoon.AI.Models;
using Silmoon.AI.OpenAI.Models;
using Silmoon.AI.OpenAI.Models.Enums;
using Silmoon.Extensions;
using Silmoon.Intelligence.Client.Models;
using Silmoon.Intelligence.Hosting.Services;
using Silmoon.Models;
using System.Collections.ObjectModel;

namespace Silmoon.Intelligence.Client.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        readonly IntelligenceService intelligenceService;
        AgentClient? currentAgentClient;
        int chatViewVersion;

        public event Action? ChatHistoryLoaded;

        public bool IsLoadingHistory { get; private set; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendChatCommand))]
        public partial string UserInput { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendChatCommand))]
        public partial bool IsBusy { get; set; }

        [ObservableProperty]
        public partial string ModelName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string UsageInfo { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string RunStatus { get; set; } = "启动中";

        [ObservableProperty]
        public partial bool IsToolActivityActive { get; set; }

        [ObservableProperty]
        public partial int ToolStreamTokenCount { get; set; }

        [ObservableProperty]
        public partial ChatSessionItem? SelectedSession { get; set; }

        [ObservableProperty]
        public partial ChatSessionItem? RenameTopicSession { get; set; }

        [ObservableProperty]
        public partial bool IsUndoHistoryConfirmationPending { get; set; }

        public string UndoHistoryButtonText => IsUndoHistoryConfirmationPending ? "确认回退" : "回退";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmRenameTopicCommand))]
        public partial string RenameTopicText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsGeneratingRenameTopic { get; set; }

        public bool IsRenameTopicDialogVisible => RenameTopicSession is not null;

        public bool HasToolIndicators => ToolExecuteIndicators.Count > 0;

        public string ToolStreamInfo => ToolStreamTokenCount > 0
            ? $"工具 token 流：约 {ToolStreamTokenCount:N0}"
            : string.Empty;

        public ObservableCollection<ChatSessionItem> Sessions { get; } = [];

        public ObservableCollection<ChatMessageItem> Messages { get; } = [];

        public ObservableCollection<ToolExecuteIndicatorItem> ToolExecuteIndicators { get; } = [];

        public MainWindowViewModel(IntelligenceService intelligenceService)
        {
            this.intelligenceService = intelligenceService;
            this.intelligenceService.ReadyResetEvent.WaitOne();
            RunStatus = "就绪";

            LoadSessions();
            SelectSession(Sessions.FirstOrDefault());
        }

        partial void OnSelectedSessionChanged(ChatSessionItem? value)
        {
            foreach (var item in Sessions)
                item.IsSelected = item == value;
            IsUndoHistoryConfirmationPending = false;
            SendChatCommand.NotifyCanExecuteChanged();
            UndoHistoryCommand.NotifyCanExecuteChanged();
            SwitchChat(value?.Id);
        }

        [RelayCommand]
        public void SelectSession(ChatSessionItem? session)
        {
            if (IsBusy) return;
            if (session is null)
            {
                SelectedSession = null;
                return;
            }

            if (SelectedSession?.Id == session.Id)
            {
                SwitchChat(session.Id);
                return;
            }
            SelectedSession = session;
        }

        partial void OnIsBusyChanged(bool value)
        {
            if (value)
                IsUndoHistoryConfirmationPending = false;
            SendChatCommand.NotifyCanExecuteChanged();
            NewChatCommand.NotifyCanExecuteChanged();
            UndoHistoryCommand.NotifyCanExecuteChanged();
            OpenRenameTopicCommand.NotifyCanExecuteChanged();
            ConfirmRenameTopicCommand.NotifyCanExecuteChanged();
            AutoGenerateRenameTopicCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsUndoHistoryConfirmationPendingChanged(bool value)
        {
            OnPropertyChanged(nameof(UndoHistoryButtonText));
        }

        partial void OnRenameTopicSessionChanged(ChatSessionItem? value)
        {
            OnPropertyChanged(nameof(IsRenameTopicDialogVisible));
            OpenRenameTopicCommand.NotifyCanExecuteChanged();
            ConfirmRenameTopicCommand.NotifyCanExecuteChanged();
            AutoGenerateRenameTopicCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsGeneratingRenameTopicChanged(bool value)
        {
            ConfirmRenameTopicCommand.NotifyCanExecuteChanged();
            AutoGenerateRenameTopicCommand.NotifyCanExecuteChanged();
        }

        void LoadSessions()
        {
            Sessions.Clear();
            foreach (var pair in intelligenceService.AgentClients.OrderByDescending(x => x.Value.State.LastAt))
            {
                var agent = pair.Value;
                Sessions.Add(new ChatSessionItem
                {
                    Id = pair.Key,
                    ChatCounting = agent.History.Count,
                    CreatedAt = agent.State.CreatedAt,
                    LastAt = agent.State.LastAt,
                    Topic = agent.State.Topic,
                });
            }
        }

        void SwitchChat(string chatId)
        {
            UnbindCurrentAgentEvents();
            chatViewVersion++;
            IsLoadingHistory = true;
            Messages.Clear();
            ToolExecuteIndicators.Clear();
            IsToolActivityActive = false;
            ToolStreamTokenCount = 0;
            NotifyToolPanelChanged();
            UsageInfo = string.Empty;
            ModelName = string.Empty;
            currentAgentClient = null;

            if (chatId is null)
            {
                IsLoadingHistory = false;
                ChatHistoryLoaded?.Invoke();
                return;
            }

            if (!intelligenceService.AgentClients.TryGetValue(chatId, out var agentClient))
            {
                IsLoadingHistory = false;
                ChatHistoryLoaded?.Invoke();
                return;
            }

            currentAgentClient = agentClient;
            ModelName = agentClient.NativeClient.ModelName;
            RunStatus = "就绪";
            BindCurrentAgentEvents();
            try
            {
                foreach (var message in CreateMessages(agentClient))
                    Messages.Add(message);
            }
            finally
            {
                IsLoadingHistory = false;
            }

            ChatHistoryLoaded?.Invoke();
        }

        ObservableCollection<ChatMessageItem> CreateMessages(AgentClient agentClient)
        {
            var messages = new ObservableCollection<ChatMessageItem>();
            foreach (var item in agentClient.History)
            {
                if (item is not NativeMessageContent message) continue;
                if (item.Role is not Role.Assistant and not Role.User) continue;

                var content = GetDisplayContent(message);
                var reasoningContent = message.ReasoningContent ?? string.Empty;
                message.ToolCalls?.Each(toolCall =>
                {
                    var functionName = toolCall.Function?.Name ?? "unknown";
                    content += content.EndsWith("\r\n") ? $"\r\n[tool call: {functionName}]\r\n" : $"\r\n\r\n[tool call: {functionName}]\r\n";
                });

                if (item.Role != Role.User && content.IsNullOrEmpty() && reasoningContent.IsNullOrEmpty())
                    continue;

                var last = messages.LastOrDefault();
                if (last is not null && last.Role == item.Role)
                {
                    if (!content.IsNullOrEmpty()) last.Content += $"\r\n{content}";
                    if (!reasoningContent.IsNullOrEmpty()) last.ReasoningContent += $"\r\n{reasoningContent}";
                }
                else
                {
                    messages.Add(new ChatMessageItem
                    {
                        Role = item.Role,
                        Content = content,
                        ReasoningContent = reasoningContent
                    });
                }
            }

            return messages;
        }

        static string GetDisplayContent(NativeMessageContent message)
        {
            var content = message.Content ?? string.Empty;
            if (message.Role != Role.User)
                return content;

            var userInput = IntelligenceService.GetUserRealInput(content);
            return userInput.IsNullOrEmpty() ? StripUserEnvelopeLenient(content) : userInput;
        }

        static string StripUserEnvelopeLenient(string content)
        {
            var text = content;
            while (TryStripLeadingTag(ref text, "time") || TryStripLeadingTag(ref text, "system")) { }
            return text.IsNullOrEmpty() ? content : text;

            static bool TryStripLeadingTag(ref string text, string tag)
            {
                var open = $"<{tag}>";
                var close = $"</{tag}>";
                if (!text.StartsWith(open, StringComparison.Ordinal)) return false;

                var end = text.IndexOf(close, open.Length, StringComparison.Ordinal);
                if (end < 0)
                {
                    text = text[open.Length..];
                    return true;
                }

                text = text[(end + close.Length)..];
                return true;
            }
        }

        void BindCurrentAgentEvents()
        {
            if (currentAgentClient is null) return;
            currentAgentClient.OnStreamOutput += AgentClient_OnStreamOutput;
            currentAgentClient.OnStreamOutputCompleted += AgentClient_OnStreamOutputCompleted;
            currentAgentClient.OnToolCallsStart += AgentClient_OnToolCallsStart;
            currentAgentClient.OnToolExecuting += AgentClient_OnToolExecuting;
            currentAgentClient.OnToolExecuted += AgentClient_OnToolExecuted;
            currentAgentClient.OnToolCallsFinish += AgentClient_OnToolCallsFinish;
        }

        void UnbindCurrentAgentEvents()
        {
            if (currentAgentClient is null) return;
            currentAgentClient.OnStreamOutput -= AgentClient_OnStreamOutput;
            currentAgentClient.OnStreamOutputCompleted -= AgentClient_OnStreamOutputCompleted;
            currentAgentClient.OnToolCallsStart -= AgentClient_OnToolCallsStart;
            currentAgentClient.OnToolExecuting -= AgentClient_OnToolExecuting;
            currentAgentClient.OnToolExecuted -= AgentClient_OnToolExecuted;
            currentAgentClient.OnToolCallsFinish -= AgentClient_OnToolCallsFinish;
        }

        Task AgentClient_OnToolCallsStart(ToolCallParameter[] toolCallParameters)
        {
            var version = chatViewVersion;
            Dispatcher.UIThread.Post(() =>
            {
                if (version != chatViewVersion) return;
                RunStatus = $"工具调用：{string.Join(", ", toolCallParameters.Select(x => x.FunctionName))}";
                IsToolActivityActive = true;
                NotifyToolPanelChanged();
            });
            return Task.CompletedTask;
        }

        Task AgentClient_OnToolExecuting(string functionName, ToolCallParameter toolCallParameter)
        {
            var version = chatViewVersion;
            Dispatcher.UIThread.Post(() =>
            {
                if (version != chatViewVersion) return;
                ToolExecuteIndicators.Add(new ToolExecuteIndicatorItem
                {
                    FunctionName = functionName,
                    Status = "执行中",
                    ToolCallId = toolCallParameter.ToolCallId,
                    State = "running"
                });
                RefreshToolActivity();
            });
            return Task.CompletedTask;
        }

        Task AgentClient_OnToolExecuted(string functionName, ToolCallParameter toolCallParameter, ToolCallResult toolCallResult)
        {
            var version = chatViewVersion;
            Dispatcher.UIThread.Post(() =>
            {
                if (version != chatViewVersion) return;
                var indicator = ToolExecuteIndicators.FirstOrDefault(x => x.ToolCallId == toolCallParameter.ToolCallId);
                if (indicator is null) return;

                indicator.Status = toolCallResult.Result.State ? "完成" : "错误";
                indicator.State = toolCallResult.Result.State ? "done" : "error";
                RefreshToolActivity();
            });
            return Task.CompletedTask;
        }

        Task<ToolCallResult[]> AgentClient_OnToolCallsFinish(ToolCallParameter[] toolCallParameters, ToolCallResult[] toolCallResults)
        {
            var version = chatViewVersion;
            Dispatcher.UIThread.Post(() =>
            {
                if (version != chatViewVersion) return;
                RunStatus = IsBusy ? "回复中" : "就绪";
                RefreshToolActivity();
            });
            return Task.FromResult(toolCallResults);
        }

        Task AgentClient_OnStreamOutput(StateSet<bool, ChatCompletionsChunk> chunkState)
        {
            if (!chunkState.State) return Task.CompletedTask;

            var version = chatViewVersion;
            foreach (var choice in chunkState.Data.Choices)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (version != chatViewVersion) return;
                    var last = Messages.LastOrDefault();
                    if (last is null) return;

                    if (choice.Delta?.ToolCalls is not null)
                    {
                        ToolStreamTokenCount += EstimateTokenCount(choice.Delta.ToolCalls.Sum(GetToolCallDeltaLength));
                        var pending = ToolExecuteIndicators.FirstOrDefault(x => x.ToolCallId == string.Empty);
                        if (pending is null)
                        {
                            ToolExecuteIndicators.Add(new ToolExecuteIndicatorItem
                            {
                                FunctionName = "工具调用",
                                Status = "生成中",
                                ToolCallId = string.Empty,
                                State = "pending"
                            });
                            RefreshToolActivity();
                        }
                        else
                        {
                            pending.Status = $"生成中，约 {ToolStreamTokenCount:N0} tokens";
                        }
                        OnPropertyChanged(nameof(ToolStreamInfo));

                        if (!last.StreamingReasoningContent.IsNullOrEmpty() && !last.StreamingReasoningContent.EndsWith("\r\n"))
                            last.StreamingReasoningContent += "\r\n";
                        if (!last.StreamingContent.IsNullOrEmpty() && !last.StreamingContent.EndsWith("\r\n"))
                            last.StreamingContent += "\r\n";
                        return;
                    }

                    var pendingTool = ToolExecuteIndicators.FirstOrDefault(x => x.ToolCallId == string.Empty);
                    if (pendingTool is not null)
                    {
                        pendingTool.Status = "完成";
                        pendingTool.State = "done";
                        RefreshToolActivity();
                    }

                    var thinking = choice.Delta?.GetThinking();
                    var content = choice.Delta?.Content;
                    if (!thinking.IsNullOrEmpty())
                    {
                        if (!last.StreamingContent.IsNullOrEmpty() && !last.StreamingContent.EndsWith("\r\n"))
                            last.StreamingContent += "\r\n";
                        last.StreamingReasoningContent += thinking;
                    }

                    if (!content.IsNullOrEmpty())
                    {
                        if (!last.StreamingReasoningContent.IsNullOrEmpty() && !last.StreamingReasoningContent.EndsWith("\r\n"))
                            last.StreamingReasoningContent += "\r\n";
                        last.StreamingContent += content;
                    }
                });
            }

            return Task.CompletedTask;
        }

        Task AgentClient_OnStreamOutputCompleted(Result result)
        {
            var version = chatViewVersion;
            Dispatcher.UIThread.Post(() =>
            {
                if (version != chatViewVersion) return;
                ToolExecuteIndicators.Clear();
                IsToolActivityActive = false;
                ToolStreamTokenCount = 0;
                NotifyToolPanelChanged();
                var last = Messages.LastOrDefault();
                if (last is not null)
                {
                    if (result.FinishReason == "stop")
                    {
                        last.Content = last.StreamingContent.TrimEnd();
                        last.ReasoningContent = last.StreamingReasoningContent.TrimEnd();
                        last.StreamingContent = string.Empty;
                        last.StreamingReasoningContent = string.Empty;
                        last.IsStreaming = false;
                    }
                    else
                    {
                        if (!last.StreamingContent.IsNullOrEmpty() && !last.StreamingContent.EndsWith("\r\n"))
                            last.StreamingContent += "\r\n";
                        if (!last.StreamingReasoningContent.IsNullOrEmpty() && !last.StreamingReasoningContent.EndsWith("\r\n"))
                            last.StreamingReasoningContent += "\r\n";
                        last.IsStreaming = true;
                    }
                }

                if (result.Usage is not null)
                    UsageInfo = $"total tokens: {result.Usage.TotalTokens:N0}, prompt tokens: {result.Usage.PromptTokens:N0}, completion tokens: {result.Usage.CompletionTokens:N0}";

                RunStatus = "就绪";
            });
            return Task.CompletedTask;
        }

        void RefreshToolActivity()
        {
            IsToolActivityActive = ToolExecuteIndicators.Any(x => x.IsActive);
            NotifyToolPanelChanged();
        }

        void NotifyToolPanelChanged()
        {
            OnPropertyChanged(nameof(HasToolIndicators));
            OnPropertyChanged(nameof(ToolStreamInfo));
        }

        static int EstimateTokenCount(int characterCount)
        {
            if (characterCount <= 0) return 1;
            return Math.Max(1, (int)Math.Ceiling(characterCount / 4.0));
        }

        static int GetToolCallDeltaLength(ToolCall toolCall)
        {
            return (toolCall.Id?.Length ?? 0)
                + (toolCall.Type?.Length ?? 0)
                + (toolCall.Function?.Name?.Length ?? 0)
                + (toolCall.Function?.Arguments?.Length ?? 0);
        }

        AgentClient? GetSelectedAgent()
        {
            return SelectedSession is null
                ? null
                : intelligenceService.AgentClients.GetValueOrDefault(SelectedSession.Id);
        }

        bool CanSendChat() => !IsBusy && GetSelectedAgent() is not null && !string.IsNullOrWhiteSpace(UserInput);

        bool CanUndoHistory() => !IsBusy && GetSelectedAgent() is not null;

        [RelayCommand(CanExecute = nameof(CanSendChat))]
        async Task SendChatAsync()
        {
            if (SelectedSession is null) return;
            var session = SelectedSession;
            var agent = GetSelectedAgent();
            if (agent is null) return;

            var input = UserInput.Trim();
            if (input.IsNullOrEmpty()) return;

            Messages.Add(new ChatMessageItem { Role = Role.User, Content = input });
            Messages.Add(new ChatMessageItem { Role = Role.Assistant, IsStreaming = true });
            UserInput = string.Empty;
            UsageInfo = string.Empty;
            IsBusy = true;
            RunStatus = "回复中";

            try
            {
                var result = await intelligenceService.Chat(input, session.Id, true);
                if (result?.Usage is not null)
                    UsageInfo = $"total tokens: {result.Usage.TotalTokens:N0}, prompt tokens: {result.Usage.PromptTokens:N0}, completion tokens: {result.Usage.CompletionTokens:N0}";

                session.ChatCounting = agent.History.Count;
                session.LastAt = agent.State.LastAt;
                if (!agent.State.Topic.IsNullOrEmpty() && session.Topic != agent.State.Topic)
                    session.Topic = agent.State.Topic;
            }
            finally
            {
                IsBusy = false;
                RunStatus = "就绪";
            }
        }

        [RelayCommand(CanExecute = nameof(CanUndoHistory))]
        void UndoHistory()
        {
            var agent = GetSelectedAgent();
            if (agent is null || SelectedSession is null) return;

            if (!IsUndoHistoryConfirmationPending)
            {
                IsUndoHistoryConfirmationPending = true;
                return;
            }

            agent.RollbackHistory();
            Messages.Clear();
            foreach (var message in CreateMessages(agent))
                Messages.Add(message);
            SelectedSession.ChatCounting = agent.History.Count;
            SelectedSession.LastAt = agent.State.LastAt;
            IsUndoHistoryConfirmationPending = false;
        }

        [RelayCommand(CanExecute = nameof(CanOpenRenameTopic))]
        void OpenRenameTopic(ChatSessionItem? session)
        {
            if (session is null) return;

            RenameTopicSession = session;
            RenameTopicText = IsAutoTopic(session.Topic) ? string.Empty : session.Topic;
        }

        [RelayCommand(CanExecute = nameof(CanConfirmRenameTopic))]
        async Task ConfirmRenameTopicAsync()
        {
            var session = RenameTopicSession;
            var topic = RenameTopicText.Trim();
            if (session is null || topic.IsNullOrEmpty() || IsGeneratingRenameTopic) return;

            var renamed = await intelligenceService.RenameAgentTopic(session.Id, topic);
            if (!renamed.IsNullOrEmpty())
                session.Topic = renamed;

            RenameTopicSession = null;
            RenameTopicText = string.Empty;
        }

        [RelayCommand]
        void CancelRenameTopic()
        {
            if (IsGeneratingRenameTopic) return;

            RenameTopicSession = null;
            RenameTopicText = string.Empty;
        }

        [RelayCommand(CanExecute = nameof(CanAutoGenerateRenameTopic))]
        async Task AutoGenerateRenameTopicAsync()
        {
            var session = RenameTopicSession;
            if (session is null || IsGeneratingRenameTopic) return;

            IsGeneratingRenameTopic = true;
            try
            {
                var generated = await intelligenceService.GenerateAgentTopicSuggestion(session.Id);
                if (!generated.IsNullOrEmpty())
                    RenameTopicText = generated.Trim();
            }
            finally
            {
                IsGeneratingRenameTopic = false;
            }
        }

        public void DeleteSession(ChatSessionItem? session)
        {
            if (session is null || IsBusy) return;

            intelligenceService.DeleteAgent(session.Id);
            Sessions.Remove(session);
            if (SelectedSession == session)
                SelectSession(Sessions.FirstOrDefault());
        }

        [RelayCommand(CanExecute = nameof(CanCreateChat))]
        void NewChat()
        {
            var newChat = intelligenceService.NewAgent();
            if (!newChat.State) return;

            var session = new ChatSessionItem
            {
                Id = newChat.Data.Id,
                ChatCounting = newChat.Data.History.Count,
                CreatedAt = DateTime.Now,
                LastAt = DateTime.Now,
                Topic = newChat.Data.State.Topic,
            };
            Sessions.Insert(0, session);
            SelectSession(session);
        }

        bool CanCreateChat() => !IsBusy;

        bool CanOpenRenameTopic(ChatSessionItem? session) => !IsBusy && RenameTopicSession is null && session is not null;

        bool CanConfirmRenameTopic() => RenameTopicSession is not null && !IsGeneratingRenameTopic && !RenameTopicText.Trim().IsNullOrEmpty();

        bool CanAutoGenerateRenameTopic() => RenameTopicSession is not null && !IsGeneratingRenameTopic;

        static bool IsAutoTopic(string topic) =>
            topic.StartsWith('#') || topic.StartsWith("新对话", StringComparison.Ordinal);

    }
}
