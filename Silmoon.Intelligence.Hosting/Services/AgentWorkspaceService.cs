using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Silmoon.AI.Models.OpenAI.Enums;
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
            get => field ??= ServiceProvider.GetRequiredService<IntelligenceService>();
            private set;
        }
        IServiceProvider ServiceProvider { get; set; }
        ISilmoonPlatformDirectoryService SilmoonPlatformDirectoryService { get; set; }

        public AgentWorkspaceService(IServiceProvider serviceProvider, ISilmoonPlatformDirectoryService silmoonPlatformDirectoryService = null) : base(Path.Combine(silmoonPlatformDirectoryService?.AppDataDirectory ?? string.Empty, "workspace"))
        {
            ServiceProvider = serviceProvider;
            SilmoonPlatformDirectoryService = silmoonPlatformDirectoryService;
        }
        public StateSet<bool, JObject> SaveChatHistory(bool overwritten)
        {
            var mainChatHistory = IntelligenceService.MainChatAgentClient.NativeChatClient.MessageHistory;
            var json = mainChatHistory.ToJsonString(SseHttpClient.SerializerSettings);
            bool fileExists = File.Exists(MainAgentChatHistoryFile);
            if (!fileExists || (fileExists && overwritten))
            {
                File.WriteAllText(MainAgentChatHistoryFile, json);
                return true.ToStateSet(JObject.FromObject(new { count = mainChatHistory.Count }), "Chat history saved successfully.");
            }
            else
            {
                return false.ToStateSet<JObject>(null, "Chat history file already exists. Set overwritten to true to overwrite it.");
            }
        }
        public StateSet<bool, JObject> RestoreChatHistory()
        {
            if (File.Exists(MainAgentChatHistoryFile))
            {
                var systemMessage = IntelligenceService.MainChatAgentClient.NativeChatClient.MessageHistory.FirstOrDefault(m => m.Role == Role.System);
                var chatHistory = JsonConvert.DeserializeObject<IMessage[]>(File.ReadAllText(MainAgentChatHistoryFile), SseHttpClient.SerializerSettings);
                //var chatHistory = JsonHelperV2.LoadJsonFromFile<IMessage[]>(MainAgentChatHistoryFile);

                if (chatHistory.FirstOrDefault(m => m.Role == Role.System) == null && systemMessage != null)
                    chatHistory = [.. (IMessage[])[systemMessage], .. chatHistory];


                IntelligenceService.MainChatAgentClient.NativeChatClient.MessageHistory = [.. chatHistory];
                return true.ToStateSet(JObject.FromObject(new { count = chatHistory.Length }),
                $"restore {chatHistory.Length} chat histories ");
            }
            return false.ToStateSet<JObject>(null, "Chat history file does not exist.");
        }
    }
}
