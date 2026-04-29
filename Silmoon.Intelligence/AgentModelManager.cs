using Silmoon.AI.OpenAI;
using Silmoon.AI.Models;
using Silmoon.AI.Models.OpenAI.Enums;
using Silmoon.AI.Models.OpenAI.Models;
using Silmoon.Extensions;
using Silmoon.Models;
using System.Collections.Concurrent;
using System;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence
{
    public class AgentModelManager
    {
        public Dictionary<string, ModelProvider> ModelProviders { get; private set; } = [];
        public Dictionary<string, NativeChatClient> NativeChatClients { get; private set; } = [];
        public ConcurrentDictionary<string, AgentClient> WorkerAgentClients { get; private set; } = [];


        public AgentModelManager(Dictionary<string, ModelProvider> models)
        {
            ModelProviders = models;
            foreach (var provider in ModelProviders)
            {
                foreach (var model in provider.Value.Models)
                {
                    NativeChatClients.Add($"{provider.Key}_{model.Name}", new NativeChatClient(provider.Value.ApiUrl, provider.Value.ApiKey, model.Name));
                }
            }
        }

        public ModelProvider[] GetAgentModelProviders() => [.. ModelProviders.Values];
        public async Task<StateSet<bool, Result>> CallSingletonAgent(string providerName, string modelName, string content, string system = null, bool enableThinking = false)
        {
            var nativeChatClient = NativeChatClients.GetValueOrDefault($"{providerName}_{modelName}");
            if (nativeChatClient is not null)
                return await CallAgentModel($"{providerName}:{modelName}", nativeChatClient, content, system, enableThinking);
            else return false.ToStateSet<Result>(null, $"specified model ({providerName},{modelName}) not found");
        }
        public StateSet<bool> ResetSingletonAgentHistory(string providerName, string modelName)
        {
            var nativeChatClient = NativeChatClients.GetValueOrDefault($"{providerName}_{modelName}");
            if (nativeChatClient is not null)
            {
                nativeChatClient.ResetHistory();
                return true.ToStateSet("success");
            }
            else return false.ToStateSet($"specified model ({providerName},{modelName}) not found");
        }
        private static async Task<StateSet<bool, Result>> CallAgentModel(string agentName, NativeChatClient nativeChatClient, string content, string system, bool enableThinking)
        {
            nativeChatClient.EnableThinking = enableThinking;
            if (system is not null) nativeChatClient.SystemPrompt = system;

            List<Chunk> chunks = [];
            Console.WriteLineWithColor($"Agent({agentName}) response start:", ConsoleColor.Green, ConsoleColor.Blue);
            await foreach (var chunk in nativeChatClient.CompletionsStreamAsync([MessageContent.Create(Role.User, content)], chunks))
            {
                if (chunk.State)
                {
                    chunk.Data.Choices.Each(x =>
                    {
                        if (x.Delta?.ToolCalls is not null) Console.Write(".");
                        else
                        {
                            Console.WriteWithColor(x?.Delta?.GetThinking(), ConsoleColor.DarkGray);
                            Console.WriteWithColor(x?.Delta?.Content, ConsoleColor.White);
                        }
                    });
                }
                else Console.WriteLineWithColor(chunk.Message);
            }
            Console.WriteLine();
            Console.WriteLineWithColor($"Agent({agentName}) response end:", ConsoleColor.Green, ConsoleColor.Blue);

            return true.ToStateSet(Result.Create([.. chunks], enableThinking));
        }

        public AgentClient[] GetWorkerAgentClients() => [.. WorkerAgentClients.Values];
        public AgentClient GetWorkerAgentClient(string name)
        {
            if (name.IsNullOrEmpty()) return null;
            return WorkerAgentClients.GetValueOrDefault(name);
        }
        public StateSet<bool, AgentClient> CreateWorkerAgent(string providerName, string modelName, string name, string roleMandate, string systemPrompt)
        {
            if (providerName.IsNullOrEmpty()) return false.ToStateSet<AgentClient>(null, "providerName is required");
            if (modelName.IsNullOrEmpty()) return false.ToStateSet<AgentClient>(null, "modelName is required");
            if (name.IsNullOrEmpty()) return false.ToStateSet<AgentClient>(null, "name is required");

            var modelProvider = ModelProviders.GetValueOrDefault(providerName);
            if (modelProvider is null) return false.ToStateSet<AgentClient>(null, $"specified model provider ({providerName}) not found");
            var modelExists = modelProvider.Models.Any(x => x.Name == modelName);
            if (!modelExists) return false.ToStateSet<AgentClient>(null, $"specified model ({modelName}) not found in provider ({providerName})");

            var workerClient = new AgentClient(modelProvider, modelName, name, roleMandate, systemPrompt);
            var result = WorkerAgentClients.TryAdd(name, workerClient);
            if (result) return true.ToStateSet(workerClient);

            var existing = WorkerAgentClients.GetValueOrDefault(name);
            return false.ToStateSet(existing, $"worker agent with the same name ({name}) already exists");
        }
        public async Task<StateSet<bool, Result>> CallWorkerAgent(string name, string content)
        {
            if (name.IsNullOrEmpty()) return false.ToStateSet<Result>(null, "name is required");
            if (content.IsNullOrEmpty()) return false.ToStateSet<Result>(null, "content is required");

            var workerClient = WorkerAgentClients.GetValueOrDefault(name);
            if (workerClient is not null)
            {
                return true.ToStateSet(await workerClient.Chat(content));
            }
            else return false.ToStateSet<Result>(null, $"specified worker agent ({name}) not found");
        }
        public StateSet<bool> ResetWorkerAgentHistory(string name)
        {
            if (name.IsNullOrEmpty()) return false.ToStateSet("name is required");

            var workerClient = WorkerAgentClients.GetValueOrDefault(name);
            if (workerClient is not null)
            {
                workerClient.NativeChatClient.ResetHistory();
                return true.ToStateSet("success");
            }
            else return false.ToStateSet($"specified worker agent ({name}) not found");
        }
        public StateSet<bool> RemoveWorkerAgent(string name)
        {
            if (name.IsNullOrEmpty()) return false.ToStateSet("name is required");

            var result = WorkerAgentClients.TryRemove(name, out var _);
            if (result) return true.ToStateSet("success");
            else return false.ToStateSet($"specified worker agent ({name}) not found");
        }
    }
}