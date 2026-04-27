using CommunityToolkit.Mvvm.ComponentModel;
using Silmoon.AI.Models.OpenAI.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.MauiClient.Models
{
    public partial class ChatItem : ObservableObject
    {
        [ObservableProperty]
        public partial Role Role { get; set; }
        [ObservableProperty]
        public partial string Content { get; set; }
        public ChatItem()
        {

        }
    }
}
