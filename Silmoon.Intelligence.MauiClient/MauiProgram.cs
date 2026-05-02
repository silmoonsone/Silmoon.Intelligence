using Microsoft.Extensions.Logging;
using Silmoon.Extensions.Hosting.Extensions;
using FluentIcons.Maui;
using Silmoon.Intelligence.Hosting.Services;
using Silmoon.Intelligence.MauiClient.Services;
using Silmoon.Maui.Platforms.Services;
using Silmoon.Maui.Services;

namespace Silmoon.Intelligence.MauiClient
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder.Services.AddSingleton<ContextManagerService>();
            builder.Services.AddSingleton<IntelligenceService>();
            builder.Services.AddHostedService(provider => provider.GetRequiredService<IntelligenceService>());
            builder.Services.AddSingleton<IFileService, FileService>();
            builder.Services.AddSilmoonConfigure<SilmoonConfigureServiceImpl, SilmoonConfigureFileReadServiceImpl>(o =>
            {
#if DEBUG
                o.DebugConfig();
#else
                o.ReleaseConfig();
#endif
            });

            builder
                .UseMauiApp<App>()
                .UseFluentIcons()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
