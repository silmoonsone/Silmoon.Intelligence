using Microsoft.Extensions.DependencyInjection;
using Silmoon.Intelligence.Hosting.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.Hosting.Extensions
{
    public static class ServiceCollectionExtension
    {
        public static void AddSilmoonIntelligence(this IServiceCollection services)
        {
            services.AddSingleton<IntelligenceService>();
            services.AddSingleton<AgentWorkspaceService>();
            services.AddSingleton<ModelContextService>();

            services.AddHostedService(provider => provider.GetRequiredService<IntelligenceService>());
        }
    }
}
