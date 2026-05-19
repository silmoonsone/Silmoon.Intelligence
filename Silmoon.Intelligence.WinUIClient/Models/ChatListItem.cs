using CommunityToolkit.Mvvm.ComponentModel;
using Silmoon.Intelligence.WinUIClient.Pages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.WinUIClient.Models
{
    public partial class ChatListItem : ObservableObject
    {
        [ObservableProperty]
        public partial Guid Id { get; set; }
        [ObservableProperty]
        public partial string DisplayName { get; set; }
        [ObservableProperty]
        public partial int ChatCounting { get; set; }
        [ObservableProperty]
        public partial DateTime CreatedAt { get; set; }
        [ObservableProperty]
        public partial DateTime LatestAt { get; set; }
        [ObservableProperty]
        public partial ChatPageViewModel This { get; set; }
    }
}
