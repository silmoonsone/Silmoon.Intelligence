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
using System.Text.RegularExpressions;

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

        public AgentWorkspaceService(IServiceProvider serviceProvider, ISilmoonConfigureService silmoonConfigureService, ISilmoonPlatformDirectoryService silmoonPlatformDirectoryService = null) : base(Path.Combine(silmoonPlatformDirectoryService?.AppDataDirectory ?? string.Empty, "workspaces"))
        {
            ServiceProvider = serviceProvider;
            SilmoonConfigureService = silmoonConfigureService as SilmoonConfigureServiceImpl;
            UpgradeLegacyAgentStateFileNames();
            UpgradeLegacyAgentStateFileContents();
        }
        public StateSet<bool, JObject> SaveAgentState(string id, bool overwritten)
        {
            if (Guid.TryParse(id, out var guid)) id = guid.ToString("N");
            if (IntelligenceService.AgentClients.TryGetValue(id, out var agentClient))
            {
                var agentState = agentClient.State;
                var json = agentState.ToJsonString(NativeApiJson.SerializerSettings);
                string filePath = Path.Combine(MainAgentMemoryDirectory, $"agentState-{id}.json");
                bool fileExists = File.Exists(filePath);
                if (!fileExists || (fileExists && overwritten))
                {
                    File.WriteAllText(filePath, json, new UTF8Encoding(true));
                    return true.ToStateSet(JObject.FromObject(new { count = agentState.NativeHistory.Length }), "Chat history saved successfully.");
                }
                else return false.ToStateSet<JObject>(null, "Chat history file already exists. Set overwritten to true to overwrite it.");
            }
            else return false.ToStateSet<JObject>(null, "Agent not found");
        }
        public StateSet<bool, AgentState> RestoreAgentState(string id)
        {
            if (Guid.TryParse(id, out var guid)) id = guid.ToString("N");
            if (IntelligenceService.AgentClients.TryGetValue(id, out var agentClient))
            {
                string filePath = Path.Combine(MainAgentMemoryDirectory, $"agentState-{id}.json");
                if (!File.Exists(filePath)) filePath = Path.Combine(MainAgentMemoryDirectory, $"agent_{id}_chat_history.json");
                if (File.Exists(filePath))
                {
                    var systemMessage = agentClient.NativeClient.MessageHistory.FirstOrDefault(m => m.Role == Role.System);
                    var agentState = LoadAgentStateFile(filePath);

                    var nativeMessage = agentState.NativeHistory;
                    if (nativeMessage.FirstOrDefault(m => m.Role == Role.System) == null && systemMessage != null) nativeMessage = [.. (INativeMessage[])[systemMessage], .. nativeMessage];

                    agentClient.State = agentState;
                    agentClient.Id = agentState.Id;
                    agentClient.NativeClient.MessageHistory = [.. nativeMessage];
                    agentClient.NativeClient.ModelProvider = SilmoonConfigureService.ModelProviders[agentState.ProviderName];
                    agentClient.NativeClient.ModelName = agentState.ModelName;
                    agentClient.NativeClient.RebuildHttpClient();

                    return true.ToStateSet(agentState, $"restore {nativeMessage.Length} chat histories ");
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
                    var agentHistory = LoadAgentStateFile(filePath);
                    histories.Add(agentHistory);
                }
            }
            return [.. histories.OrderBy(h => h.LastAt)];
        }
        public StateSet<bool> DeleteAgentStateFile(string id)
        {
            if (Guid.TryParse(id, out var guid)) id = guid.ToString("N");
            string filePath = Path.Combine(MainAgentMemoryDirectory, $"agentState-{id}.json");
            if (!File.Exists(filePath)) filePath = Path.Combine(MainAgentMemoryDirectory, $"agent_{id}_chat_history.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true.ToStateSet("Chat history deleted successfully.");
            }
            else return false.ToStateSet("Agent history file does not exist.");
        }
        Dictionary<string, string> GetHistoryStateFileNames()
        {
            var historyFiles = Directory.GetFiles(MainAgentMemoryDirectory, "agentState-*.json");
            var result = new Dictionary<string, string>();
            foreach (var file in historyFiles)
            {
                var fileName = Path.GetFileName(file);
                if (fileName.StartsWith("agentState-") && fileName.EndsWith(".json"))
                {
                    var idPart = fileName.Substring(11, fileName.Length - 11 - 5);
                    if (!idPart.IsNullOrEmpty())
                        result[idPart] = fileName;
                }
            }
            return result;
        }
        static AgentState LoadAgentStateFile(string filePath)
        {
            var json = File.ReadAllText(filePath);
            var agentState = JsonConvert.DeserializeObject<AgentState>(json, NativeApiJson.SerializerSettings);
            if (agentState is null) throw new JsonException($"Cannot deserialize agent state file: {filePath}");
            agentState.NativeHistory ??= [];
            return agentState;
        }

        static string GetShortId(string id)
        {
            if (id.IsNullOrEmpty()) return Guid.NewGuid().ToString("N")[..8];
            return id.Length <= 8 ? id : id[..8];
        }


        void UpgradeLegacyAgentStateFileNames()
        {
            var memoryFiles = Directory.GetFiles(MainAgentMemoryDirectory, "*.json");
            foreach (var filePath in memoryFiles)
            {
                var fileName = Path.GetFileName(filePath);
                var normalizedFileName = Regex.Replace(fileName, "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", match => Guid.Parse(match.Value).ToString("N"));
                if (normalizedFileName.StartsWith("agent_") && normalizedFileName.EndsWith("_chat_history.json"))
                {
                    var idPart = normalizedFileName.Substring(6, normalizedFileName.Length - 6 - 18);
                    normalizedFileName = $"agentState-{idPart}.json";
                }
                var targetFilePath = Path.Combine(MainAgentMemoryDirectory, normalizedFileName);
                if (!string.Equals(filePath, targetFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(targetFilePath))
                    {
                        if (File.GetLastWriteTimeUtc(filePath) > File.GetLastWriteTimeUtc(targetFilePath))
                            File.Copy(filePath, targetFilePath, overwrite: true);
                        File.Delete(filePath);
                    }
                    else File.Move(filePath, targetFilePath);
                }
            }
        }
        void UpgradeLegacyAgentStateFileContents()
        {
            var memoryFiles = Directory.GetFiles(MainAgentMemoryDirectory, "*.json");
            foreach (var filePath in memoryFiles)
            {
                try
                {
                    var jsonToken = JToken.Parse(File.ReadAllText(filePath));
                    var changed = false;
                    if (jsonToken is JArray historyJson)
                    {
                        UpgradeLegacyNativeMessageJson(historyJson);
                        jsonToken = JObject.FromObject(CreateAgentStateFromLegacyHistory(filePath, historyJson), JsonSerializer.Create(NativeApiJson.SerializerSettings));
                        changed = true;
                    }
                    else if (jsonToken is JObject stateJson)
                    {
                        changed = UpgradeLegacyAgentStateJson(stateJson, GetAgentStateIdFromFileName(filePath));
                    }
                    if (changed)
                        File.WriteAllText(filePath, jsonToken.ToJsonString(NativeApiJson.SerializerSettings), new UTF8Encoding(true));
                }
                catch (JsonException)
                {
                }
            }
        }

        AgentState CreateAgentStateFromLegacyHistory(string filePath, JArray historyJson)
        {
            var id = GetAgentStateIdFromFileName(filePath);
            if (Guid.TryParse(id, out var guid)) id = guid.ToString("N");
            if (id.IsNullOrEmpty()) id = Guid.NewGuid().ToString("N");

            var nativeHistory = historyJson.ToObject<INativeMessage[]>(JsonSerializer.Create(NativeApiJson.SerializerSettings)) ?? [];
            var result = new AgentState(SilmoonConfigureService.DefaultProvider.ProviderName, SilmoonConfigureService.DefaultModelName)
            {
                Id = id,
                NativeHistory = nativeHistory,
                LastAt = File.GetLastWriteTime(filePath),
            };
            if (result.Topic.IsNullOrEmpty()) result.Topic = $"新对话({GetShortId(id)})";
            return result;
        }
        static string GetAgentStateIdFromFileName(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            if (fileName.StartsWith("agentState-") && fileName.EndsWith(".json"))
                return fileName.Substring(11, fileName.Length - 11 - 5);
            if (fileName.StartsWith("agent_") && fileName.EndsWith("_chat_history.json"))
                return fileName.Substring(6, fileName.Length - 6 - 18);
            return null;
        }
        static bool UpgradeLegacyAgentStateJson(JObject stateJson, string fallbackId = null)
        {
            var changed = false;
            var id = stateJson["Id"]?.Value<string>();
            var idValue = id;
            if (idValue.IsNullOrEmpty())
                idValue = fallbackId;

            if (Guid.TryParse(idValue, out var guid) && stateJson["Id"]?.Value<string>() != guid.ToString("N"))
            {
                stateJson["Id"] = guid.ToString("N");
                changed = true;
            }

            var nativeHistoryJson = stateJson[nameof(AgentState.NativeHistory)] ?? stateJson["ChatHistory"];
            if (nativeHistoryJson is not null)
            {
                changed |= UpgradeLegacyNativeMessageJson(nativeHistoryJson);
                stateJson[nameof(AgentState.NativeHistory)] = nativeHistoryJson;
                changed |= stateJson.Remove("ChatHistory");
            }
            return changed;
        }
        static bool UpgradeLegacyNativeMessageJson(JToken historyJson)
        {
            if (historyJson is not JArray history) return false;

            var changed = false;
            foreach (var messageJson in history.OfType<JObject>())
            {
                var typeName = messageJson["$type"]?.Value<string>();
                if (!typeName.IsNullOrEmpty())
                {
                    var upgradedTypeName = typeName
                        .Replace("Silmoon.AI.Models.OpenAI.Models.", "Silmoon.AI.OpenAI.Models.")
                        .Replace("Silmoon.AI.Models.OpenAI.Enums.", "Silmoon.AI.OpenAI.Models.Enums.")
                        .Replace(".MessageContent", ".NativeMessageContent")
                        .Replace(".MessageJson", ".NativeMessageJson")
                        .Replace(".MessageImageUrl", ".NativeMessageImageUrl")
                        .Replace(".MessageText", ".NativeMessageText")
                        .Replace(".Messages`1", ".NativeMessages`1")
                        .Replace(".Message`1", ".NativeMessage`1")
                        .Replace(".Message,", ".NativeMessage,");
                    if (upgradedTypeName != typeName)
                    {
                        messageJson["$type"] = upgradedTypeName;
                        changed = true;
                    }
                }

                var idJson = messageJson["id"];
                if (idJson is null && messageJson.TryGetValue("hash", out var hashJson))
                    idJson = hashJson;
                if (idJson is null && messageJson.TryGetValue("Hash", out var pascalHashJson))
                    idJson = pascalHashJson;

                if (idJson is not null)
                {
                    var id = idJson.Value<string>();
                    var normalizedId = id;
                    if (Guid.TryParse(id, out var messageId))
                        normalizedId = messageId.ToString("N");

                    if (messageJson["id"]?.Value<string>() != normalizedId)
                    {
                        messageJson["id"] = normalizedId;
                        changed = true;
                    }
                }

                changed |= messageJson.Remove("hash");
                changed |= messageJson.Remove("Hash");
            }
            return changed;
        }
    }
}
