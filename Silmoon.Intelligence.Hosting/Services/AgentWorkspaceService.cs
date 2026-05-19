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
        SilmoonConfigureServiceImpl SilmoonConfigureService { get; set; }

        public AgentWorkspaceService(IServiceProvider serviceProvider, ISilmoonConfigureService silmoonConfigureService, ISilmoonPlatformDirectoryService silmoonPlatformDirectoryService = null) : base(Path.Combine(silmoonPlatformDirectoryService?.AppDataDirectory ?? string.Empty, "workspace"))
        {
            ServiceProvider = serviceProvider;
            SilmoonConfigureService = silmoonConfigureService as SilmoonConfigureServiceImpl;
        }
        public StateSet<bool, JObject> SaveAgentHistory(Guid id, bool overwritten)
        {
            if (IntelligenceService.AgentClients.TryGetValue(id, out var agentClient))
            {
                var chatHistory = agentClient.NativeChatClient.MessageHistory;
                var agentHistory = new AgentHistory
                {
                    Id = id,
                    ProviderName = agentClient.NativeChatClient.ModelProvider.ProviderName,
                    ModelName = agentClient.NativeChatClient.ModelName,
                    ChatHistory = [.. chatHistory]
                };
                var json = agentHistory.ToJsonString(SseHttpClient.SerializerSettings);
                string filePath = Path.Combine(MainAgentMemoryDirectory, $"agent_{id}_chat_history.json");
                bool fileExists = File.Exists(filePath);
                if (!fileExists || (fileExists && overwritten))
                {
                    File.WriteAllText(filePath, json);
                    return true.ToStateSet(JObject.FromObject(new { count = chatHistory.Count }), "Chat history saved successfully.");
                }
                else return false.ToStateSet<JObject>(null, "Chat history file already exists. Set overwritten to true to overwrite it.");
            }
            else return false.ToStateSet<JObject>(null, "Agent not found");
        }
        public StateSet<bool, AgentHistory> RestoreAgentHistory(Guid id)
        {
            if (IntelligenceService.AgentClients.TryGetValue(id, out var agentClient))
            {
                string filePath = Path.Combine(MainAgentMemoryDirectory, $"agent_{id}_chat_history.json");
                if (File.Exists(filePath))
                {
                    var systemMessage = agentClient.NativeChatClient.MessageHistory.FirstOrDefault(m => m.Role == Role.System);
                    var agentHistory = JsonConvert.DeserializeObject<AgentHistory>(File.ReadAllText(filePath), SseHttpClient.SerializerSettings);

                    var chatHistory = agentHistory.ChatHistory;
                    if (chatHistory.FirstOrDefault(m => m.Role == Role.System) == null && systemMessage != null) chatHistory = [.. (IMessage[])[systemMessage], .. chatHistory];

                    agentClient.NativeChatClient.MessageHistory = [.. chatHistory];
                    agentClient.Id = agentHistory.Id;
                    agentClient.NativeChatClient.ModelProvider = SilmoonConfigureService.ModelProviders[agentHistory.ProviderName];
                    agentClient.NativeChatClient.ModelName = agentHistory.ModelName;

                    return true.ToStateSet(agentHistory, $"restore {chatHistory.Length} chat histories ");
                }
                else return false.ToStateSet<AgentHistory>(null, "Agent history file does not exist.");
            }
            else return false.ToStateSet<AgentHistory>(null, "Agent not found");
        }
        public StateSet<bool> DeleteAgentHistory(Guid id)
        {
            string filePath = Path.Combine(MainAgentMemoryDirectory, $"agent_{id}_chat_history.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true.ToStateSet("Chat history deleted successfully.");
            }
            else return false.ToStateSet("Agent history file does not exist.");
        }

        public Dictionary<Guid, string> GetHistoryFileNames()
        {
            var historyFiles = Directory.GetFiles(MainAgentMemoryDirectory, "agent_*_chat_history.json");
            var result = new Dictionary<Guid, string>();
            foreach (var file in historyFiles)
            {
                var fileName = Path.GetFileName(file);
                if (fileName.StartsWith("agent_") && fileName.EndsWith("_chat_history.json"))
                {
                    var idPart = fileName.Substring(6, fileName.Length - 6 - 18);
                    if (Guid.TryParse(idPart, out var id))
                    {
                        result[id] = fileName;
                    }
                }
            }
            return result;
        }
    }
    public class AgentHistory
    {
        public Guid Id { get; set; }
        public string ProviderName { get; set; }
        public string ModelName { get; set; }
        public IMessage[] ChatHistory { get; set; }
    }
}
