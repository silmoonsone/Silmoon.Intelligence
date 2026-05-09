using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Silmoon.Extensions.Hosting.Extensions;
using Silmoon.Extensions.Hosting.Interfaces;
using Silmoon.Intelligence.ConsoleTesting.Services;
using Silmoon.Intelligence.Hosting.Extensions;
using Silmoon.Intelligence.Hosting.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSilmoonIntelligence();
builder.Services.AddSingleton<ConsoleService>();
builder.Services.AddSingleton<ISilmoonPlatformDirectoryService, SilmoonPlatformDirectoryServiceImpl>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<ConsoleService>());
builder.Services.AddSilmoonConfigure<SilmoonConfigureServiceImpl>(o =>
{
#if DEBUG
    o.DebugConfig();
#else
    o.ReleaseConfig();
#endif
});

var host = builder.Build();
await host.RunAsync();

