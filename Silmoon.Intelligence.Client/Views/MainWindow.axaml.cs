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
        bool shouldAutoScroll = true;

        public MainWindow()
        {
            InitializeComponent();
            AddHandler(PointerPressedEvent, MainWindow_PointerPressed, RoutingStrategies.Tunnel);
            ChatInput.AddHandler(KeyDownEvent, ChatInput_KeyDown, RoutingStrategies.Tunnel);
            ChatInput.TextChanged += (_, _) => Dispatcher.UIThread.Post(UpdateChatInputHeight, DispatcherPriority.Background);
            ChatInput.PropertyChanged += ChatInput_PropertyChanged;
            ChatScrollViewer.ScrollChanged += ChatScrollViewer_ScrollChanged;
            DataContextChanged += MainWindow_DataContextChanged;
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

                    ScrollChatToBottom(true);
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
                ScrollChatToBottom(e.NewItems?.OfType<ChatMessageItem>().Any(x => x.IsUser) == true);
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

        void ChatInput_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == nameof(Bounds))
                Dispatcher.UIThread.Post(UpdateChatInputHeight, DispatcherPriority.Background);
        }

        void UpdateChatInputHeight()
        {
            var lineCount = ChatInput.GetLineCount();
            if (lineCount < 1)
                lineCount = 1;

            var targetHeight = Math.Min(150, 34 + (lineCount - 1) * 20);
            if (Math.Abs(ChatInput.Height - targetHeight) > 0.5)
                ChatInput.Height = targetHeight;
        }

        void ChatScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            var maxOffset = Math.Max(0, ChatScrollViewer.Extent.Height - ChatScrollViewer.Viewport.Height);
            if (double.IsNaN(maxOffset) || double.IsInfinity(maxOffset))
                return;

            shouldAutoScroll = maxOffset - ChatScrollViewer.Offset.Y <= 24;
        }

        void ScrollChatToBottom(bool force = false)
        {
            if (!force && !shouldAutoScroll)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                var maxOffset = Math.Max(0, ChatScrollViewer.Extent.Height - ChatScrollViewer.Viewport.Height);
                if (double.IsNaN(maxOffset) || double.IsInfinity(maxOffset))
                    return;

                ChatScrollViewer.Offset = new Vector(ChatScrollViewer.Offset.X, maxOffset);
                shouldAutoScroll = true;
            }, DispatcherPriority.Background);
        }
    }
}
