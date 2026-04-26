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
            var window = new Window(new AppShell())
            {
                Width = 1200,
                Height = 800,
            };
            IntelligenceService.StartAsync(CancellationToken.None).Wait();
            return window;
        }
    }
}