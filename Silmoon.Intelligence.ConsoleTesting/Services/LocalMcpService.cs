using Silmoon.AI.Client.OpenAI;
using Silmoon.AI.Interfaces;
using Silmoon.AI.Tools;
using Silmoon.Extensions;
using Silmoon.Extensions.Hosting.Interfaces;
using Silmoon.Intelligence.Core;
using Silmoon.Intelligence.Core.Tools;
using System;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.ConsoleTesting.Services
{
    public class LocalMcpService
    {
        public List<IExecuteTool> ExecuteTools { get; set; } = [
            new FileTool(),
            new CommandTool(),
            new WaitTool(),
            ];
        SilmoonConfigureServiceImpl SilmoonConfigureService { get; set; }
        ModelsManager ModelsManager { get; set; }
        public LocalMcpService(ISilmoonConfigureService silmoonConfigureService)
        {
            SilmoonConfigureService = silmoonConfigureService as SilmoonConfigureServiceImpl;
            ModelsManager = new ModelsManager(SilmoonConfigureService.Models);
        }
        public void InjectMcp(NativeChatClient nativeChatClient)
        {
            ExecuteTools.Add(new DeepThinkTool(nativeChatClient));
            ExecuteTools.Add(new MemoryTool(nativeChatClient));
            ExecuteTools.Add(new ModelTool(ModelsManager));

            string systemPrompt = SilmoonConfigureService.SystemPrompt;
            if (systemPrompt is not null) nativeChatClient.SystemPrompt += "\r\n" + systemPrompt;

            foreach (var tool in ExecuteTools)
            {
                tool.InjectToolCall(nativeChatClient);
            }
        }
    }
}
