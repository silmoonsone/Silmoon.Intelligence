using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.WinUIClient.Models
{
    public partial class ToolExecuteIndicatorModel : ObservableObject
    {
        [ObservableProperty]
        public required partial string FunctionName { get; set; }
        [ObservableProperty]
        public required partial string Status { get; set; }
        [ObservableProperty]
        public required partial string ToolCallId { get; set; }
        [ObservableProperty]
        public required partial Brush Color { get; set; }
    }
}

