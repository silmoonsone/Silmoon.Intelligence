using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Silmoon.AI;
using Silmoon.AI.OpenAI.Models.Enums;
using Silmoon.AI.OpenAI.Models;
using Silmoon.Extensions;
using Silmoon.Extensions.Hosting.Interfaces;
using Silmoon.Intelligence.Models;
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
        public StateSet<bool, JObject> SaveAgentState(Guid id, bool overwritten)
        {
            if (IntelligenceService.AgentClients.TryGetValue(id, out var agentClient))
            {
                var agentHistory = agentClient.State;
                var json = agentHistory.ToJsonString(NativeApiJson.SerializerSettings);
                string filePath = Path.Combine(MainAgentMemoryDirectory, $"agent_{id}_chat_history.json");
                bool fileExists = File.Exists(filePath);
                if (!fileExists || (fileExists && overwritten))
                {
                    File.WriteAllText(filePath, json);
                    return true.ToStateSet(JObject.FromObject(new { count = agentClient.State.ChatHistory.Length }), "Chat history saved successfully.");
                }
                else return false.ToStateSet<JObject>(null, "Chat history file already exists. Set overwritten to true to overwrite it.");
            }
            else return false.ToStateSet<JObject>(null, "Agent not found");
        }
        public StateSet<bool, AgentState> RestoreAgentState(Guid id)
        {
            if (IntelligenceService.AgentClients.TryGetValue(id, out var agentClient))
            {
                string filePath = Path.Combine(MainAgentMemoryDirectory, $"agent_{id}_chat_history.json");
                if (File.Exists(filePath))
                {
                    var systemMessage = agentClient.NativeChatClient.MessageHistory.FirstOrDefault(m => m.Role == Role.System);
                    var agentHistory = JsonConvert.DeserializeObject<AgentState>(File.ReadAllText(filePath), NativeApiJson.SerializerSettings);

                    var chatHistory = agentHistory.ChatHistory;
                    if (chatHistory.FirstOrDefault(m => m.Role == Role.System) == null && systemMessage != null) chatHistory = [.. (IMessage[])[systemMessage], .. chatHistory];

                    agentClient.NativeChatClient.MessageHistory = [.. chatHistory];
                    agentClient.Id = agentHistory.Id;
                    agentClient.NativeChatClient.ModelProvider = SilmoonConfigureService.ModelProviders[agentHistory.ProviderName];
                    agentClient.NativeChatClient.ModelName = agentHistory.ModelName;
                    agentClient.NativeChatClient.RebuildHttpClient();

                    return true.ToStateSet(agentHistory, $"restore {chatHistory.Length} chat histories ");
                }
                else return false.ToStateSet<AgentState>(null, "Agent history file does not exist.");
            }
            else return false.ToStateSet<AgentState>(null, "Agent not found");
        }

        public List<AgentState> GetAgentStates()
        {
            var historyFileNames = GetHistoryStateFileNames();
            List<AgentState> histories = [];
            foreach (var kvp in historyFileNames)
            {
                var filePath = Path.Combine(MainAgentMemoryDirectory, kvp.Value);
                if (File.Exists(filePath))
                {
                    var agentHistory = JsonConvert.DeserializeObject<AgentState>(File.ReadAllText(filePath), NativeApiJson.SerializerSettings);
                    histories.Add(agentHistory);
                }
            }
            return [.. histories.OrderBy(h => h.LastAt)];
        }
        public StateSet<bool> DeleteAgentStateFile(Guid id)
        {
            string filePath = Path.Combine(MainAgentMemoryDirectory, $"agent_{id}_chat_history.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true.ToStateSet("Chat history deleted successfully.");
            }
            else return false.ToStateSet("Agent history file does not exist.");
        }

        Dictionary<Guid, string> GetHistoryStateFileNames()
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
}


