using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Silmoon.AI.Models;
using Silmoon.AI.OpenAI.Models.Enums;
using Silmoon.AI.OpenAI.Models;
using Silmoon.AI.OpenAI;
using Silmoon.Extensions;
using Silmoon.Extensions.Hosting.Interfaces;
using Silmoon.Intelligence.Hosting.Tools;
using Silmoon.Intelligence.Models;
using Silmoon.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.Hosting.Services
{
    public class IntelligenceService : BackgroundService
    {
        //public AgentClient FastAgentClient { get; set; }
        public AgentClient SupervisorAgentClient { get; set; }
        public AgentClient DefaultChatAgentClient { get; set; }
        public Dictionary<string, AgentClient> AgentClients { get; set; } = [];
        ModelContextService ModelContextService { get; set; }
        SilmoonConfigureServiceImpl SilmoonConfigureService { get; set; }
        AgentWorkspaceService AgentWorkspaceService { get; set; }
        public ManualResetEvent ReadyResetEvent { get; set; } = new ManualResetEvent(false);

        public IntelligenceService(ISilmoonConfigureService silmoonConfigureService, ModelContextService modelContextService, AgentWorkspaceService agentWorkspaceService)
        {
            SilmoonConfigureService = silmoonConfigureService as SilmoonConfigureServiceImpl;
            ModelContextService = modelContextService;
            AgentWorkspaceService = agentWorkspaceService;
        }

        public StateSet<bool, AgentClient> NewAgent() => NewAgent(SilmoonConfigureService.DefaultProvider, SilmoonConfigureService.DefaultModelName);
        public StateSet<bool, AgentClient> NewAgent(ModelProvider modelProvider, string modelName)
        {
            var unifiedSystemPrompt = File.ReadAllText(Path.Combine(AgentWorkspaceService.WorkspaceDirectory, "system_prompts", "unified_agent_system.md"));

            var agentClient = new AgentClient(modelProvider, modelName, $"\r\n{unifiedSystemPrompt}\r\n", SilmoonConfigureService.NativeClientDisableProxy, true);
            AgentClients[agentClient.Id] = agentClient;
            ModelContextService.InjectMainChatTools(agentClient.NativeClient);
            return true.ToStateSet(agentClient);
        }
        public StateSet<bool> DeleteAgent(string id)
        {
            var find = AgentClients.TryGetValue(id, out var agent);
            if (find)
            {
                agent.Dispose();
                AgentWorkspaceService.DeleteAgentStateFile(id);
                AgentClients.Remove(id);
                return true.ToStateSet();
            }
            else return false.ToStateSet("Agent not found");
        }

        public async Task<Result> Chat(string input, bool autoSave = false) => await Chat(input, DefaultChatAgentClient.Id, autoSave);
        public async Task<Result> Chat(string input, string id, bool autoSave = false)
        {
            if (AgentClients.TryGetValue(id, out var agent))
            {
                var result = await agent.Chat($"<time>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</time>{input}");
                if (agent.State.Topic.StartsWith("新对话") && agent.History.Count > 2 && autoSave) await GenerateAgentTopic(id);
                if (autoSave)
                    SaveChatState(id);
                return result;
            }
            return null;
        }
        public async Task<string> GenerateAgentTopic(string id)
        {
            if (AgentClients.TryGetValue(id, out var agent))
            {
                var topic = await GenerateAgentTopicSuggestion(agent);
                if (!topic.IsNullOrEmpty()) agent.State.Topic = topic;
                SaveChatState(id);
                return topic;
            }
            return null;
        }
        public async Task<string> GenerateAgentTopicSuggestion(string id)
        {
            if (AgentClients.TryGetValue(id, out var agent))
                return await GenerateAgentTopicSuggestion(agent);
            return null;
        }
        async Task<string> GenerateAgentTopicSuggestion(AgentClient agent)
        {
            var topicResult = await SupervisorAgentClient.Chat($"根据用户和AI的聊天信息和用户沟通意图，生成一个简短的描述小标题，3-8个文字，如果是英文的沟通信息，可以生成3-5个单词标题，不需要任何格式字符包括但不限于markdown，不得换行，只是一个简短的标题，3-10个字：{string.Join("\n", agent.History.TakeLast(10).ToJsonString())}");
            return GetUserRealInput(topicResult.Content).Trim();
        }
        public async Task<string> RenameAgentTopic(string id, string topic)
        {
            if (AgentClients.TryGetValue(id, out var agent))
            {
                if (!topic.IsNullOrEmpty()) agent.State.Topic = topic;
                SaveChatState(id);
                return topic;
            }
            return null;
        }

        public StateSet<bool, JObject> SaveChatState(string id) => AgentWorkspaceService.SaveAgentState(id, overwritten: true);
        public StateSet<bool, AgentState> RestoreChatState(string id) => AgentWorkspaceService.RestoreAgentState(id);

        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var supervisorAdditionSystemPrompt = File.ReadAllText(Path.Combine(AgentWorkspaceService.WorkspaceDirectory, "system_prompts", "supervisor_agent_system.md"));
            var unifiedSystemPrompt = File.ReadAllText(Path.Combine(AgentWorkspaceService.WorkspaceDirectory, "system_prompts", "unified_agent_system.md"));

            SupervisorAgentClient = new AgentClient(SilmoonConfigureService.DefaultProvider, SilmoonConfigureService.DefaultModelName, $"\r\n{supervisorAdditionSystemPrompt}\r\n{unifiedSystemPrompt}\r\n", SilmoonConfigureService.NativeClientDisableProxy, false);
            //FastAgentClient = new AgentClient(SilmoonConfigureService.DefaultProvider, SilmoonConfigureService.DefaultModelName, $"\r\n{supervisorAdditionSystemPrompt}\r\n{unifiedSystemPrompt}\r\n", SilmoonConfigureService.NativeClientDisableProxy, false);

            ModelContextService.InjectSupervisorTools(SupervisorAgentClient.NativeClient);

            var agentStates = AgentWorkspaceService.GetAgentStates();
            if (agentStates.IsNullOrEmpty()) NewAgent();
            else
            {
                foreach (var agentState in agentStates)
                {
                    var modelProvider = SilmoonConfigureService.ModelProviders.Get(agentState.ProviderName, SilmoonConfigureService.DefaultProvider);
                    var agentClient = new AgentClient(agentState, modelProvider, agentState.ModelName, $"\r\n{unifiedSystemPrompt}\r\n", disableProxy: SilmoonConfigureService.NativeClientDisableProxy, enableThinking: true);
                    ModelContextService.InjectMainChatTools(agentClient.NativeClient);
                    agentClient.NativeClient.MessageHistory = [.. agentState.NativeHistory];
                    AgentClients[agentClient.Id] = agentClient;
                }
            }

            DefaultChatAgentClient = AgentClients.LastOrDefault().Value;
            ReadyResetEvent.Set();
        }


        public static string GetUserRealInput(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var s = input;
            while (TryStripLeadingTag(ref s, "time") || TryStripLeadingTag(ref s, "system")) { }
            return s;

            static bool TryStripLeadingTag(ref string text, string tag)
            {
                var open = $"<{tag}>";
                var close = $"</{tag}>";
                if (!text.StartsWith(open, StringComparison.Ordinal)) return false;
                var end = text.IndexOf(close, open.Length, StringComparison.Ordinal);
                if (end < 0) return false;
                text = text[(end + close.Length)..];
                return true;
            }
        }
        public override void Dispose()
        {
            foreach (var kvpAgent in AgentClients)
            {
                kvpAgent.Value?.Dispose();
            }
            SupervisorAgentClient?.Dispose();
        }
    }
}

