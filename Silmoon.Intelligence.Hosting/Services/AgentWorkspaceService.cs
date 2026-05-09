using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Silmoon.AI.Models.OpenAI.Models;
using Silmoon.AI.OpenAI;
using Silmoon.Extensions;
using Silmoon.Extensions.Hosting.Interfaces;
using Silmoon.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.Hosting.Services
{
    public class AgentWorkspaceService : AgentWorkspaceManager
    {
        public IntelligenceService IntelligenceService
        {
            get
            {
                if (field is null) IntelligenceService = ServiceProvider.GetRequiredService<IntelligenceService>();
                return field;
            }
            private set;
        }
        IServiceProvider ServiceProvider { get; set; }
        ISilmoonPlatformDirectoryService SilmoonPlatformDirectoryService { get; set; }

        public AgentWorkspaceService(IServiceProvider serviceProvider, ISilmoonPlatformDirectoryService silmoonPlatformDirectoryService = null) : base(Path.Combine(silmoonPlatformDirectoryService?.AppDataDirectory ?? string.Empty, "workspace"))
        {
            ServiceProvider = serviceProvider;
            SilmoonPlatformDirectoryService = silmoonPlatformDirectoryService;
        }
        public StateSet<bool> SaveChatHistory(bool overwritten)
        {
            var mainChatHistory = IntelligenceService.AgentClient.NativeChatClient.MessageHistory;
            var json = mainChatHistory.ToJsonString(SseHttpClient.SerializerSettings);
            bool fileExists = File.Exists(MainAgentChatHistoryFile);
            if (!fileExists || (fileExists && overwritten))
            {
                File.WriteAllText(MainAgentChatHistoryFile, json);
                return new StateSet<bool>(true);
            }
            else
            {
                return false.ToStateSet("Chat history file already exists. Set overwritten to true to overwrite it.");
            }
        }
        public StateSet<bool> RestoreChatHistory()
        {
            if (File.Exists(MainAgentChatHistoryFile))
            {
                var chatHistory = JsonHelperV2.LoadJsonFromFile<MessageContent[]>(MainAgentChatHistoryFile);
                IntelligenceService.AgentClient.NativeChatClient.MessageHistory = [.. chatHistory];
                return true.ToStateSet($"restore {chatHistory.Length} chat histories ");
            }
            return false.ToStateSet("Chat history file does not exist.");
        }
    }
}
