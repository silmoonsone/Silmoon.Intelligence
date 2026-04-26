using Newtonsoft.Json.Linq;
using Silmoon.AI.Handlers;
using Silmoon.AI.Models;
using Silmoon.AI.Models.OpenAI.Models;
using Silmoon.AI.OpenAI;
using Silmoon.AI.Prompts;
using Silmoon.Extensions;
using Silmoon.Models;
using System;
using System.Collections.Concurrent;
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
            NativeChatClient.OnNativeClientChatFinished += NativeChatClient_OnNativeClientChatFinished;
            NativeChatClient.Tools.Add(Tool.Create("ToolCallTestTool", "This is a test tool_calling test tool.", []));

        }

        private Task NativeChatClient_OnNativeClientChatFinished(Result result)
        {
            StreamOutputFinishedAction?.Invoke(result);
            return Task.CompletedTask;
        }

        private async Task<ConcurrentDictionary<string, ToolCallResult>> NativeChatClient_OnToolCallCompleted(ConcurrentDictionary<string, ToolCallResult> toolCallResults)
        {
            return await (ToolCallCompletedHandler?.Invoke(toolCallResults) ?? Task.FromResult(toolCallResults));
        }
        private async Task<List<ToolCallResult>> NativeChatClient_OnToolCallStart(ToolCallParameter[] toolCallParameters, ConcurrentDictionary<string, ToolCallResult> toolCallResults)
        {
            return await (ToolCallStartHandler?.Invoke(toolCallParameters, toolCallResults) ?? Task.FromResult<List<ToolCallResult>>(null));
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
                return result;
            });
        }
    }
}