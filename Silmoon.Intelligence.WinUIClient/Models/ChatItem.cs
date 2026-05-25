using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Silmoon.AI.Models.OpenAI.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.WinUIClient.Models
{
    public partial class ChatItem : ObservableObject
    {
        [ObservableProperty]
        public partial Role Role { get; set; }
        [ObservableProperty]
        public partial List<ImageSource> Images { get; set; }
        [ObservableProperty]
        public partial string StreamingContent { get; set; }
        [ObservableProperty]
        public partial bool StreamingContentVisual { get; set; }
        [ObservableProperty]
        public partial string Content { get; set; }
        [ObservableProperty]
        public partial bool ContentVisual { get; set; }
        public ChatItem()
        {

        }
        public ChatItem(Role role, string finishContent)
        {
            Role = role;
            Content = finishContent;
            ContentVisual = true;
        }
        public override string ToString() => $"Role: {Role}, FinishContent: {Content}, {Images?.Count ?? 0} images";

    }
}
