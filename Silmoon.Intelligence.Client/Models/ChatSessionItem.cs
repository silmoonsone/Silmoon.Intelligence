using CommunityToolkit.Mvvm.ComponentModel;

namespace Silmoon.Intelligence.Client.Models
{
    public partial class ChatSessionItem : ObservableObject
    {
        [ObservableProperty]
        public partial Guid Id { get; set; }

        [ObservableProperty]
        public partial string Topic { get; set; } = string.Empty;

        [ObservableProperty]
        public partial int ChatCounting { get; set; }

        [ObservableProperty]
        public partial DateTime CreatedAt { get; set; }

        [ObservableProperty]
        public partial DateTime LastAt { get; set; }

        [ObservableProperty]
        public partial bool IsSelected { get; set; }

        public string LastAtText => LastAt.ToString("MM-dd HH:mm");

        partial void OnLastAtChanged(DateTime value) => OnPropertyChanged(nameof(LastAtText));
    }
}
