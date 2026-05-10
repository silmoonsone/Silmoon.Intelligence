using Microsoft.Extensions.DependencyInjection;
using Silmoon.AI.Interfaces;
using Silmoon.AI.OpenAI;
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
        public void InjectMainChatTools(NativeChatClient nativeChatClient)
        {
            AddTools(nativeChatClient, [
                new WorldStateTool(),
                new FileTool(),
                new CommandTool(),
                new WaitTool(),
                new CSharpTool(),
                new MemoryTool(nativeChatClient),
                new AgentModelTool(AgentModelManager),
                new GithubTool(),
                ]);

            string systemPrompt = SilmoonConfigureService.SystemPrompt;
            if (systemPrompt is not null) nativeChatClient.SystemPrompt = $"{systemPrompt}\r\n{nativeChatClient.SystemPrompt}";
        }
        public void InjectSupervisorTools(NativeChatClient nativeChatClient)
        {
            AddTools(nativeChatClient, [
                new WorldStateTool(),
                new FileTool(),
                new WaitTool(),
                new CSharpTool(),
                new MemoryTool(nativeChatClient),
                new AgentModelTool(AgentModelManager),
                new AgentStateTool(IntelligenceService)
                ]);

            string systemPrompt = SilmoonConfigureService.SystemPrompt;
            if (systemPrompt is not null) nativeChatClient.SystemPrompt = $"{systemPrompt}\r\n{nativeChatClient.SystemPrompt}";
        }
    }
}
