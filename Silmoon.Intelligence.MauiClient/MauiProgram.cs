using Microsoft.Extensions.Logging;
using Silmoon.Extensions.Hosting.Extensions;
using FluentIcons.Maui;
using Silmoon.Extensions.Hosting.Interfaces;
using Silmoon.Intelligence.Hosting.Services;
using Silmoon.Intelligence.MauiClient.Services;
using Silmoon.Maui.Platforms.Services;
using Silmoon.Maui.Services;
using Silmoon.Intelligence.Hosting.Extensions;

namespace Silmoon.Intelligence.MauiClient
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder.Services.AddSilmoonIntelligence();
            builder.Services.AddSingleton<IFileService, FileService>();
            builder.Services.AddSingleton<ISilmoonPlatformDirectoryService, SilmoonPlatformDirectoryServiceImpl>();
            builder.Services.AddSilmoonConfigure<SilmoonConfigureServiceImpl>(o =>
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

