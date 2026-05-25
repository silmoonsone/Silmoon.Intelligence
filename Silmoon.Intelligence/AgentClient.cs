using Newtonsoft.Json.Linq;
using Silmoon.AI;
using Silmoon.AI.Models;
using Silmoon.AI.Models.OpenAI.Models;
using Silmoon.AI.OpenAI;
using Silmoon.AI.Prompts;
using Silmoon.Extensions;
using Silmoon.Intelligence.Models;
using Silmoon.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence
{
    public class AgentClient : IDisposable
    {
        public NativeChatClient NativeChatClient { get; private set; }

        public event ToolCallsStartHandler OnToolCallsStart;
        public event ToolCallInvokeHandler OnToolCallInvoke;
        public event ToolExecutingHandler OnToolExecuting;
        public event ToolExecutedHandler OnToolExecuted;
        public event ToolCallsFinishHandler OnToolCallsFinish;
        public event StreamOutputHandler OnStreamOutput;
        public event StreamOutputCompletedHandler OnStreamOutputCompleted;

        public Guid Id { get; set; } = Guid.Empty;
        public string Name { get; set; } = string.Empty;
        public string RoleMandate { get; set; } = string.Empty;
        public string Topic { get => State.Topic; set => State.Topic = value; }
        public List<IMessage> History { get => NativeChatClient.MessageHistory; set => NativeChatClient.MessageHistory = value; }
        public bool IsBusy { get; set; } = false;
        public AgentState State { get; set; }

        public AgentClient(Guid id, ModelProvider modelProvider, string modelName, string name, string roleMandate, string systemPrompt = StringHelper.EmptyString, AgentState state = null, bool disableProxy = false)
        {
            Id = id;
            Name = name;
            RoleMandate = roleMandate ?? string.Empty;
            State = state is null ? new AgentState(id, modelProvider.ProviderName, modelName) : state;

            NativeChatClient = new NativeChatClient(modelProvider, modelName, $"{UtilPrompt.ContextPrompt}\r\n{systemPrompt}", disableProxy);
            NativeChatClient.OnToolCallsStart += async (toolCallParameters) => await (OnToolCallsStart is null ? Task.CompletedTask : OnToolCallsStart.Invoke(toolCallParameters));
            NativeChatClient.OnToolCallInvoke += async (toolCallParameter, toolCallResult) => await (OnToolCallInvoke is null ? Task.FromResult(toolCallResult) : OnToolCallInvoke.Invoke(toolCallParameter, toolCallResult));
            NativeChatClient.OnToolExecuting += async (functionName, toolCallParameter) => await (OnToolExecuting is null ? Task.CompletedTask : OnToolExecuting.Invoke(functionName, toolCallParameter));
            NativeChatClient.OnToolExecuted += async (functionName, toolCallParameter, toolCallResult) => await (OnToolExecuted is null ? Task.CompletedTask : OnToolExecuted.Invoke(functionName, toolCallParameter, toolCallResult));
            NativeChatClient.OnToolCallsFinish += async (toolCallParameters, toolCallResults) => await (OnToolCallsFinish is null ? Task.FromResult<ToolCallResult[]>(null) : OnToolCallsFinish.Invoke(toolCallParameters, toolCallResults));
            NativeChatClient.OnStreamOutput += async (chunk) => await (OnStreamOutput is null ? Task.CompletedTask : OnStreamOutput.Invoke(chunk));
            NativeChatClient.OnStreamOutputCompleted += NativeChatClient_OnStreamOutputCompleted;
        }

        private Task NativeChatClient_OnStreamOutputCompleted(Result result)
        {
            IsBusy = false;
            return OnStreamOutputCompleted?.Invoke(result) ?? Task.CompletedTask;
        }
        public async Task<Result> Chat(string input)
        {
            return await Task.Run(async () =>
            {
                if (input.IsNullOrEmpty()) return null;
                List<Chunk> chunks = [];
                IsBusy = true;
                await foreach (var chunk in NativeChatClient.CompletionsStreamAsync(input, chunks))
                {

                }
                var result = Result.Create([.. chunks]);
                State.ChatHistory = [.. NativeChatClient.MessageHistory];
                State.LastAt = DateTime.Now;
                return result;
            });
        }
        public void RollbackHistory()
        {
            NativeChatClient.RollbackHistory();
            State.ChatHistory = [.. NativeChatClient.MessageHistory];
        }

        public void Dispose()
        {
            OnToolCallsStart = null;
            OnToolCallInvoke = null;
            OnToolExecuting = null;
            OnToolExecuted = null;
            OnToolCallsFinish = null;
            OnStreamOutput = null;
            OnStreamOutputCompleted = null;
            NativeChatClient?.Dispose();
            NativeChatClient = null;
        }
    }
}