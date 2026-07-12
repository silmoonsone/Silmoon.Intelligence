using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Silmoon.Extensions.Hosting.Extensions;
using Silmoon.Extensions.Hosting.Interfaces;
using Silmoon.Intelligence.Client.Services;
using Silmoon.Intelligence.Client.ViewModels;
using Silmoon.Intelligence.Client.Views;
using Silmoon.Intelligence.Hosting.Extensions;
using Silmoon.Intelligence.Hosting.Services;

namespace Silmoon.Intelligence.Client
{
    public partial class App : Application
    {
        static IHost? Host { get; set; }

        public static T GetService<T>() where T : notnull
        {
            if (Host is null) throw new InvalidOperationException("Application host has not been initialized.");
            return Host.Services.GetRequiredService<T>();
        }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            Host = CreateHost();
            Host.StartAsync().GetAwaiter().GetResult();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownRequested += (_, _) =>
                {
                    if (Host is null) return;
                    Host.StopAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
                    Host.Dispose();
                    Host = null;
                };

                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(GetService<IntelligenceService>()),
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        static IHost CreateHost()
        {
            var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();

            builder.Services.AddSingleton<Core>();
            builder.Services.AddSingleton<ISilmoonPlatformDirectoryService, SilmoonPlatformDirectoryServiceImpl>();
            builder.Services.AddSilmoonIntelligence();
            builder.Services.AddSilmoonConfigure<SilmoonConfigureServiceImpl>(o =>
            {
#if DEBUG
                o.DebugConfig();
#else
                o.ReleaseConfig();
#endif
            });

            return builder.Build();
        }
    }
}
