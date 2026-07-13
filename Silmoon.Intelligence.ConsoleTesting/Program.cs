using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Silmoon.Extensions.Hosting.Extensions;
using Silmoon.Extensions.Hosting.Interfaces;
using Silmoon.Intelligence.ConsoleTesting.Services;
using Silmoon.Intelligence.Hosting.Extensions;
using Silmoon.Intelligence.Hosting.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSilmoonIntelligence<SilmoonPlatformDirectoryServiceImpl>();
builder.Services.AddSingleton<ConsoleService>();
//if not add ISilmoonPlatformDirectoryService or AddSilmoonIntelligence not defined ISilmoonPlatformDirectoryService, default workspaces is "workspaces" in the application base directory
//builder.Services.AddSingleton<ISilmoonPlatformDirectoryService, SilmoonPlatformDirectoryServiceImpl>();
builder.Services.AddSilmoonConfigure<SilmoonConfigureServiceImpl>(o =>
{
#if DEBUG
    o.DebugConfig();
#else
    o.ReleaseConfig();
#endif
});

builder.Services.AddHostedService(provider => provider.GetRequiredService<ConsoleService>());

var host = builder.Build();
await host.RunAsync();


