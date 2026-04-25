using Newtonsoft.Json.Linq;
using Silmoon.AI.Client.OpenAI;
using Silmoon.AI.Handlers;
using Silmoon.AI.Models;
using Silmoon.AI.Models.OpenAI.Enums;
using Silmoon.AI.Models.OpenAI.Models;
using Silmoon.AI.Prompts;
using Silmoon.Extensions;
using Silmoon.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence
{
    public class AgentClient
    {
        public NativeChatClient NativeChatClient { get; set; }
        public Action<StateSet<bool, Chunk>> StreamOutputAction { get; set; }
        public Action<Result> StreamOutputFinishedAction { get; set; }
        public ToolCallStartHandler ToolCallStartHandler { get; set; }
        public ToolCallCompletedHandler ToolCallCompletedHandler { get; set; }
        public AgentClient(ModelProvider modelProvider, string modelName)
        {
            NativeChatClient = new NativeChatClient(modelProvider, modelName, UtilPrompt.ContextPrompt);
            NativeChatClient.OnToolCallStart += NativeChatClient_OnToolCallStart;
            NativeChatClient.OnToolCallCompleted += NativeChatClient_OnToolCallCompleted;
            NativeChatClient.Tools.Add(Tool.Create("ToolCallTestTool", "This is a test tool_calling test tool.", []));

        }

        private async Task<StateSet<bool, string>> NativeChatClient_OnToolCallCompleted(StateSet<bool, string> toolCallResult)
        {
            return await (ToolCallCompletedHandler?.Invoke(toolCallResult) ?? Task.FromResult(toolCallResult));
        }
        private async Task<StateSet<bool, string>> NativeChatClient_OnToolCallStart(string functionName, JObject parameters, string toolCallId, StateSet<bool, string> toolMessageState)
        {
            return await (ToolCallStartHandler?.Invoke(functionName, parameters, toolCallId, toolMessageState) ?? Task.FromResult<StateSet<bool, string>>(null));
        }

        public async Task<Result> Chat(string input)
        {
            return await Task.Run(async () =>
            {
                if (input.IsNullOrEmpty()) return null;
                List<Chunk> chunks = [];
                await foreach (var chunk in NativeChatClient.CompletionsStreamAsync(input, chunks))
                {
                    StreamOutputAction?.Invoke(chunk);
                }
                var result = Result.Create([.. chunks]);
                StreamOutputFinishedAction?.Invoke(result);
                return result;
            });
        }
    }
}