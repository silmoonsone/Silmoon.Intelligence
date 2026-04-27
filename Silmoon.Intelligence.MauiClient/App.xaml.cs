using Microsoft.Extensions.DependencyInjection;
using Silmoon.Intelligence.MauiClient.Services;

namespace Silmoon.Intelligence.MauiClient
{
    public partial class App : Application
    {
        IntelligenceService IntelligenceService { get; set; }
        public static IServiceProvider ServiceProvider { get; set; }
        public App(IntelligenceService intelligenceService, IServiceProvider serviceProvider)
        {
            IntelligenceService = intelligenceService;
            ServiceProvider = serviceProvider;
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());

            // Fixed desktop window size should not be applied on mobile,
            // otherwise iOS keyboard insets/layout can be calculated incorrectly.
            if (DeviceInfo.Platform == DevicePlatform.WinUI || DeviceInfo.Platform == DevicePlatform.MacCatalyst)
            {
                window.Width = 1200;
                window.Height = 800;
            }
            IntelligenceService.StartAsync(CancellationToken.None).Wait();
            return window;
        }
    }
}