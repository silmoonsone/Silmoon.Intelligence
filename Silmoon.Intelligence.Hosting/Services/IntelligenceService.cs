using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Silmoon.AI.Models;
using Silmoon.AI.Models.OpenAI.Enums;
using Silmoon.AI.Models.OpenAI.Models;
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
        public AgentClient SupervisorAgentClient { get; set; }
        public AgentClient DefaultChatAgentClient { get; set; }
        public Dictionary<Guid, AgentClient> AgentClients { get; set; } = [];
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

        public StateSet<bool, KeyValuePair<Guid, AgentClient>> NewAgent() => NewAgent(SilmoonConfigureService.DefaultProvider, SilmoonConfigureService.DefaultModelName);
        public StateSet<bool, KeyValuePair<Guid, AgentClient>> NewAgent(ModelProvider modelProvider, string modelName)
        {
            var unifiedSystemPrompt = File.ReadAllText(Path.Combine(AgentWorkspaceService.WorkspaceDirectory, "system_prompts", "unified_agent_system.md"));

            var id = Guid.NewGuid();
            var agentClient = new AgentClient(id, modelProvider, modelName, $"MainAgent-{id}", $"MainAgent-{id}", $"""
                你是人工智能执行人，意思是你可以调用其他的Agent进行工作，你需要合理的分配任务给其他Agent，并且管理他们的工作进度和结果，确保任务的完成。
                {unifiedSystemPrompt}
                """, null, disableProxy: SilmoonConfigureService.NativeClientDisableProxy, enableThinking: true);
            AgentClients[id] = agentClient;
            ModelContextService.InjectMainChatTools(agentClient.NativeChatClient);
            return true.ToStateSet(new KeyValuePair<Guid, AgentClient>(id, agentClient));
        }
        public StateSet<bool> DeleteAgent(Guid id)
        {
            var find = AgentClients.TryGetValue(id, out var agent);
            if (find)
            {
                agent.Dispose();
                AgentWorkspaceService.DeleteAgentStateFile(id);
                AgentClients.Remove(id);
                return true.ToStateSet();
            }
            else
                return false.ToStateSet("Agent not found");
        }

        public async Task<Result> Chat(string input, bool autoSave = false) => await Chat(input, DefaultChatAgentClient.Id, autoSave);
        public async Task<Result> Chat(string input, Guid agentId, bool autoSave = false)
        {
            if (AgentClients.TryGetValue(agentId, out var agent))
            {
                var result = await agent.Chat($"<time>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</time>{input}");
                if (agent.Topic.IsNullOrEmpty() && agent.History.Count > 3 && autoSave) await GenerateAgentTopic(agentId);
                if (autoSave)
                    SaveChatState(agentId);
                return result;
            }
            return null;
        }
        public async Task<string> GenerateAgentTopic(Guid agentId)
        {
            if (AgentClients.TryGetValue(agentId, out var agent))
            {
                var topicResult = await SupervisorAgentClient.Chat($"根据用户和AI的聊天信息和用户沟通意图，生成一个简短的描述小标题，3-8个文字，如果是英文的沟通信息，可以生成3-5个单词标题，不需要任何格式字符包括但不限于markdown，不得换行，知识一个简短的标题，3-10个字：{string.Join("\n", agent.History.TakeLast(10).ToJsonString())}");
                var topic = GetUserRealInput(topicResult.Content).Trim();
                if (!topic.IsNullOrEmpty()) agent.Topic = topic;
                SaveChatState(agentId);
                return topic;
            }
            return null;
        }

        public StateSet<bool, JObject> SaveChatState(Guid agentId) => AgentWorkspaceService.SaveAgentState(agentId, overwritten: true);
        public StateSet<bool, AgentState> RestoreChatState(Guid agentId) => AgentWorkspaceService.RestoreAgentState(agentId);

        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var supervisorAdditionSystemPrompt = File.ReadAllText(Path.Combine(AgentWorkspaceService.WorkspaceDirectory, "system_prompts", "supervisor_agent_system.md"));
            var unifiedSystemPrompt = File.ReadAllText(Path.Combine(AgentWorkspaceService.WorkspaceDirectory, "system_prompts", "unified_agent_system.md"));

            SupervisorAgentClient = new AgentClient(Guid.NewGuid(), SilmoonConfigureService.DefaultProvider, SilmoonConfigureService.DefaultModelName, "Agent监管助手", "Agent监管员", $"""
                {supervisorAdditionSystemPrompt}
                {unifiedSystemPrompt}
                """, null, disableProxy: SilmoonConfigureService.NativeClientDisableProxy, enableThinking: false);

            ModelContextService.InjectSupervisorTools(SupervisorAgentClient.NativeChatClient);

            var agentStates = AgentWorkspaceService.GetAgentStates();
            if (agentStates.IsNullOrEmpty()) NewAgent();
            else
            {
                foreach (var agentState in agentStates)
                {
                    var modelProvider = SilmoonConfigureService.ModelProviders.Get(agentState.ProviderName, SilmoonConfigureService.DefaultProvider);
                    var modelName = agentState.ModelName;
                    var agentClient = new AgentClient(agentState.Id, modelProvider, modelName, $"Agent-{agentState.Id}", $"Agent-{agentState.Id}", $"""
                        {unifiedSystemPrompt}
                        """, agentState, disableProxy: SilmoonConfigureService.NativeClientDisableProxy, enableThinking: true);
                    ModelContextService.InjectMainChatTools(agentClient.NativeChatClient);
                    agentClient.NativeChatClient.MessageHistory = [.. agentState.ChatHistory];
                    AgentClients[agentState.Id] = agentClient;
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
