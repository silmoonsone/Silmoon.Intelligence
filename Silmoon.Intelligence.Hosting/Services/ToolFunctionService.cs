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
    public class ToolFunctionService
    {
        public List<IExecuteTool> ExecuteTools { get; set; } = [];
        SilmoonConfigureServiceImpl SilmoonConfigureService { get; set; }
        AgentModelManager AgentModelManager { get; set; }
        public ToolFunctionService(ISilmoonConfigureService silmoonConfigureService)
        {
            SilmoonConfigureService = silmoonConfigureService as SilmoonConfigureServiceImpl;
            AgentModelManager = new AgentModelManager(SilmoonConfigureService.ModelProviders);
        }
        public void InjectTools(NativeChatClient nativeChatClient)
        {
            ExecuteTools.Add(new FileTool());
            ExecuteTools.Add(new CommandTool());
            ExecuteTools.Add(new WaitTool());
            ExecuteTools.Add(new CSharpTool());

            ExecuteTools.Add(new MemoryTool(nativeChatClient));
            ExecuteTools.Add(new AgentModelTool(AgentModelManager));

            string systemPrompt = SilmoonConfigureService.SystemPrompt;
            if (systemPrompt is not null) nativeChatClient.SystemPrompt += "\r\n" + systemPrompt;

            foreach (var tool in ExecuteTools)
            {
                tool.InjectToolCall(nativeChatClient);
            }
        }
    }
}
