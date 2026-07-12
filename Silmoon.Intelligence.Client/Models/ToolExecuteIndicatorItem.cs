using CommunityToolkit.Mvvm.ComponentModel;

namespace Silmoon.Intelligence.Client.Models
{
    public partial class ToolExecuteIndicatorItem : ObservableObject
    {
        [ObservableProperty]
        public partial string FunctionName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Status { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ToolCallId { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string State { get; set; } = "running";

        public string StateText => State switch
        {
            "running" => "执行中",
            "done" => "完成",
            "error" => "错误",
            "pending" => "生成中",
            _ => State
        };

        public bool IsActive => State is "running" or "pending";

        partial void OnStateChanged(string value)
        {
            OnPropertyChanged(nameof(StateText));
            OnPropertyChanged(nameof(IsActive));
        }
    }
}
