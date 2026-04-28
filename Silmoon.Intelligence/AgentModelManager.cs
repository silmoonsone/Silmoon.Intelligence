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
        public ConcurrentDictionary<string, NativeChatClient> AgentList { get; private set; } = [];


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
    }
}
