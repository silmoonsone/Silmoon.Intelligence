using Microsoft.Extensions.DependencyInjection;
using Silmoon.AI.Interfaces;
using Silmoon.AI.Tools;
using Silmoon.Extensions.Hosting.Interfaces;
using Silmoon.Intelligence.Hosting.Tools;
using Silmoon.Intelligence.Tools;
using System;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.Hosting.Services
{
    public class ModelContextService : ModelContextManager
    {
        SilmoonConfigureServiceImpl SilmoonConfigureService { get; set; }
        AgentModelManager AgentModelManager { get; set; }
        IntelligenceService IntelligenceService
        {
            get => field ??= ServiceProvider.GetRequiredService<IntelligenceService>();
            set;
        }
        IServiceProvider ServiceProvider { get; set; }
        public ModelContextService(ISilmoonConfigureService silmoonConfigureService, IServiceProvider serviceProvider)
        {
            SilmoonConfigureService = silmoonConfigureService as SilmoonConfigureServiceImpl;
            AgentModelManager = new AgentModelManager(SilmoonConfigureService.ModelProviders);
            ServiceProvider = serviceProvider;
        }
        public void InjectMainChatTools(INativeClient nativeClient)
        {
            AddTools(nativeClient, [
                new WorldStateTool(),
                new FileTool(),
                new CommandTool(),
                new WaitTool(),
                new CSharpTool(),
                new MemoryTool(nativeClient),
                new AgentModelTool(AgentModelManager),
                new GithubTool(),
                new WebSearchTool(SilmoonConfigureService.AliyunOpenSearchKey),
                ]);

            string systemPrompt = SilmoonConfigureService.SystemPrompt;
            if (systemPrompt is not null) nativeClient.SystemPrompt = $"{systemPrompt}\r\n{nativeClient.SystemPrompt}";
        }
        public void InjectSupervisorTools(INativeClient nativeClient)
        {
            AddTools(nativeClient, [
                new WorldStateTool(),
                new FileTool(),
                new WaitTool(),
                new CSharpTool(),
                new MemoryTool(nativeClient),
                new AgentModelTool(AgentModelManager),
                new AgentStateTool(IntelligenceService),
                new WebSearchTool(SilmoonConfigureService.AliyunOpenSearchKey),
                ]);

            string systemPrompt = SilmoonConfigureService.SystemPrompt;
            if (systemPrompt is not null) nativeClient.SystemPrompt = $"{systemPrompt}\r\n{nativeClient.SystemPrompt}";
        }
    }
}



