using Silmoon.AI.OpenAI;
using Silmoon.AI.Interfaces;
using Silmoon.AI.Tools;
using Silmoon.Extensions.Hosting.Interfaces;
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
        public ModelContextService(ISilmoonConfigureService silmoonConfigureService)
        {
            SilmoonConfigureService = silmoonConfigureService as SilmoonConfigureServiceImpl;
            AgentModelManager = new AgentModelManager(SilmoonConfigureService.ModelProviders);
        }
        public void InjectTools(NativeChatClient nativeChatClient)
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
    }
}
