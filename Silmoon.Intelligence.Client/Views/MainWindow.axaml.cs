using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Silmoon.Intelligence.Client.Models;
using Silmoon.Intelligence.Client.ViewModels;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Silmoon.Intelligence.Client.Views
{
    public partial class MainWindow : Window
    {
        MainWindowViewModel? viewModel;
        int historyScrollVersion;

        public MainWindow()
        {
            InitializeComponent();
            AddHandler(PointerPressedEvent, MainWindow_PointerPressed, RoutingStrategies.Tunnel);
            ChatInput.AddHandler(KeyDownEvent, ChatInput_KeyDown, RoutingStrategies.Tunnel);
            ChatInput.TextChanged += ChatInput_TextChanged;
            DataContextChanged += MainWindow_DataContextChanged;
            UpdateChatInputHeight();
        }

        void MainWindow_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (viewModel?.IsUndoHistoryConfirmationPending != true || IsPointerInsideUndoButton(e.Source))
                return;

            viewModel.IsUndoHistoryConfirmationPending = false;
        }

        bool IsPointerInsideUndoButton(object? source)
        {
            if (source == UndoHistoryButton)
                return true;

            return source is Visual visual && visual.GetVisualAncestors().Contains(UndoHistoryButton);
        }

        void MainWindow_DataContextChanged(object? sender, EventArgs e)
        {
            if (viewModel is not null)
            {
                viewModel.ChatHistoryLoaded -= ViewModel_ChatHistoryLoaded;
                viewModel.Messages.CollectionChanged -= Messages_CollectionChanged;
                foreach (var message in viewModel.Messages)
                    message.PropertyChanged -= Message_PropertyChanged;
            }

            viewModel = DataContext as MainWindowViewModel;
            if (viewModel is null) return;

            viewModel.ChatHistoryLoaded += ViewModel_ChatHistoryLoaded;
            viewModel.Messages.CollectionChanged += Messages_CollectionChanged;
            foreach (var message in viewModel.Messages)
                message.PropertyChanged += Message_PropertyChanged;
            ViewModel_ChatHistoryLoaded();
        }

        void ViewModel_ChatHistoryLoaded()
        {
            var version = ++historyScrollVersion;
            Dispatcher.UIThread.Post(async () =>
            {
                foreach (var delay in new[] { 0, 50, 150, 300, 600 })
                {
                    if (delay > 0)
                        await Task.Delay(delay);
                    if (version != historyScrollVersion)
                        return;

                    ScrollChatToBottom();
                }
            }, DispatcherPriority.Background);
        }

        void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems is not null)
            {
                foreach (ChatMessageItem item in e.NewItems)
                    item.PropertyChanged += Message_PropertyChanged;
            }

            if (e.OldItems is not null)
            {
                foreach (ChatMessageItem item in e.OldItems)
                    item.PropertyChanged -= Message_PropertyChanged;
            }

            if (viewModel?.IsLoadingHistory != true)
                ScrollChatToBottom();
        }

        void Message_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ChatMessageItem.StreamingContent) or nameof(ChatMessageItem.StreamingReasoningContent) or nameof(ChatMessageItem.Content))
                ScrollChatToBottom();
        }

        void SessionItem_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Control control || control.DataContext is not ChatSessionItem session || viewModel is null) return;

            var point = e.GetCurrentPoint(control);
            if (!point.Properties.IsRightButtonPressed) return;

            SelectSession(session);
        }

        void SessionItem_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Control control || control.DataContext is not ChatSessionItem session) return;

            SelectSession(session);
            e.Handled = true;
        }

        void DeleteSessionMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem item || item.DataContext is not ChatSessionItem session || viewModel is null) return;

            if (!item.Classes.Contains("dangerConfirm"))
            {
                item.Header = "确认删除";
                item.Classes.Set("dangerConfirm", true);
                e.Handled = true;
                return;
            }

            viewModel.DeleteSession(session);
            ResetDeleteMenuItem(item);
            FindOwningContextMenu(item)?.Close();
            e.Handled = true;
        }

        void SessionContextMenu_Closed(object? sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu menu) return;

            foreach (var item in menu.Items.OfType<MenuItem>())
                ResetDeleteMenuItem(item);
        }

        static void ResetDeleteMenuItem(MenuItem item)
        {
            if (!item.Classes.Contains("deleteSessionMenuItem")) return;

            item.Header = "删除";
            item.Classes.Set("dangerConfirm", false);
        }

        static ContextMenu? FindOwningContextMenu(MenuItem item) =>
            item.Parent as ContextMenu ?? item.GetVisualAncestors().OfType<ContextMenu>().FirstOrDefault();

        void SelectSession(ChatSessionItem session)
        {
            viewModel?.SelectSession(session);
        }

        void ChatInput_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift) || viewModel is null)
                return;

            e.Handled = true;
            if (viewModel.SendChatCommand.CanExecute(null))
                viewModel.SendChatCommand.Execute(null);
        }

        void ChatInput_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateChatInputHeight();
        }

        void UpdateChatInputHeight()
        {
            const double singleLineHeight = 34;
            const double lineHeight = 20;
            const double maxHeight = 150;

            var text = ChatInput.Text ?? string.Empty;
            var lineCount = Math.Max(1, text.Replace("\r\n", "\n").Split('\n').Length);
            ChatInput.Height = Math.Min(maxHeight, singleLineHeight + (lineCount - 1) * lineHeight);
        }

        void ScrollChatToBottom()
        {
            Dispatcher.UIThread.Post(() =>
            {
                ChatScrollViewer.Offset = new Vector(ChatScrollViewer.Offset.X, ChatScrollViewer.Extent.Height);
            }, DispatcherPriority.Background);
        }
    }
}
