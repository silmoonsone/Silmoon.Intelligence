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
        public partial string Topic { get; set; }
        [ObservableProperty]
        public partial int ChatCounting { get; set; }
        [ObservableProperty]
        public partial DateTime CreatedAt { get; set; }
        [ObservableProperty]
        public partial DateTime LastAt { get; set; }
        [ObservableProperty]
        public partial ChatPageViewModel This { get; set; }
    }
}

