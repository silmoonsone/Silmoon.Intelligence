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
        public partial string StreamContent { get; set; }
        [ObservableProperty]
        public partial bool StreamContentVisual { get; set; }
        [ObservableProperty]
        public partial string FinishContent { get; set; }
        [ObservableProperty]
        public partial bool FinishContentVisual { get; set; }
        public ChatItem()
        {

        }
        public ChatItem(Role role, string finishContent)
        {
            Role = role;
            FinishContent = finishContent;
            FinishContentVisual = true;
        }
        public override string ToString() => $"Role: {Role}, FinishContent: {FinishContent}, {Images?.Count ?? 0} images";

    }
}
