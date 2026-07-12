using CommunityToolkit.Mvvm.ComponentModel;
using Silmoon.AI.OpenAI.Models.Enums;

namespace Silmoon.Intelligence.Client.Models
{
    public partial class ChatMessageItem : ObservableObject
    {
        [ObservableProperty]
        public partial Role Role { get; set; }

        [ObservableProperty]
        public partial string Content { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ReasoningContent { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string StreamingContent { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string StreamingReasoningContent { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsStreaming { get; set; }

        public string RoleLabel => Role == Role.User ? "YOU" : "AGENT";

        public bool IsAgent => Role == Role.Assistant;

        public bool IsUser => Role == Role.User;

        public bool HasContent => !string.IsNullOrWhiteSpace(Content);

        public bool HasReasoningContent => !string.IsNullOrWhiteSpace(ReasoningContent);

        public bool HasStreamingContent => !string.IsNullOrWhiteSpace(StreamingContent);

        public bool HasStreamingReasoningContent => !string.IsNullOrWhiteSpace(StreamingReasoningContent);

        public bool IsAgentContentVisible => IsAgent && HasContent;

        public bool HasMessageBody => HasContent || HasReasoningContent || HasStreamingContent || HasStreamingReasoningContent;

        partial void OnReasoningContentChanged(string value)
        {
            OnPropertyChanged(nameof(HasReasoningContent));
            OnPropertyChanged(nameof(HasMessageBody));
        }

        partial void OnContentChanged(string value)
        {
            OnPropertyChanged(nameof(HasContent));
            OnPropertyChanged(nameof(IsAgentContentVisible));
            OnPropertyChanged(nameof(HasMessageBody));
        }

        partial void OnStreamingContentChanged(string value)
        {
            OnPropertyChanged(nameof(HasStreamingContent));
            OnPropertyChanged(nameof(HasMessageBody));
        }

        partial void OnStreamingReasoningContentChanged(string value)
        {
            OnPropertyChanged(nameof(HasStreamingReasoningContent));
            OnPropertyChanged(nameof(HasMessageBody));
        }

        partial void OnRoleChanged(Role value)
        {
            OnPropertyChanged(nameof(RoleLabel));
            OnPropertyChanged(nameof(IsAgent));
            OnPropertyChanged(nameof(IsUser));
            OnPropertyChanged(nameof(IsAgentContentVisible));
        }
    }
}
