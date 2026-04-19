using Silmoon.AI.Client.OpenAI;
using Silmoon.AI.Models;
using Silmoon.AI.Models.OpenAI.Enums;
using Silmoon.AI.Models.OpenAI.Interfaces;
using Silmoon.AI.Models.OpenAI.Models;
using Silmoon.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.Core
{
    public class ModelsManager
    {
        public Dictionary<string, ModelConfig> Models { get; private set; } = [];
        public Dictionary<string, NativeChatClient> NativeChatClients { get; private set; } = [];
        public ModelsManager(Dictionary<string, ModelConfig> models)
        {
            Models = models;
            foreach (var model in Models)
            {
                NativeChatClients.Add(model.Key, new NativeChatClient(model.Value.ApiUrl, model.Value.ApiKey, model.Value.ModelName));
            }
        }
        public async Task<Result> Ask(string modelId, string content, string system = null)
        {
            var NativeChatClient = NativeChatClients[modelId];
            if (system is not null) NativeChatClient.SystemPrompt = system;

            List<Chunk> chunks = [];
            Console.WriteLineWithColor($"Agent({modelId}) response start:", ConsoleColor.Green, ConsoleColor.Blue);
            await foreach (var chunk in NativeChatClient.CompletionsStreamAsync([MessageContent.Create(Role.User, content)], chunks))
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
            Console.WriteLineWithColor($"Agent({modelId}) response end:", ConsoleColor.Green, ConsoleColor.Blue);
            var result = Result.Create([.. chunks]);

            return result;
        }
    }
}
