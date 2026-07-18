using Newtonsoft.Json.Linq;
using Silmoon.AI;
using Silmoon.AI.Models;
using Silmoon.AI.OpenAI.Models;
using Silmoon.AI.Interfaces;
using Silmoon.AI.Prompts;
using Silmoon.Extensions;
using Silmoon.Intelligence.Models;
using Silmoon.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics.CodeAnalysis;

namespace Silmoon.Intelligence
{
    public class AgentClient : IDisposable
    {
        public INativeClient NativeClient { get; private set; }

        public event ToolCallsStartHandler OnToolCallsStart;
        public event ToolCallInvokeHandler OnToolCallInvoke;
        public event ToolExecutingHandler OnToolExecuting;
        public event ToolExecutedHandler OnToolExecuted;
        public event ToolCallsFinishHandler OnToolCallsFinish;
        public event StreamOutputHandler OnStreamOutput;
        public event StreamOutputCompletedHandler OnStreamOutputCompleted;

        public required Guid Id { get; set; } = Guid.Empty;
        public string Name { get; set; } = string.Empty;
        public string RoleMandate { get; set; } = string.Empty;
        public string Topic { get => State.Topic; set => State.Topic = value; }
        public NativeMessageCollection History { get => NativeClient.MessageHistory; set => NativeClient.MessageHistory = value; }
        public bool IsBusy { get; set; } = false;
        public AgentState State { get; set; }

        [SetsRequiredMembers]
        public AgentClient(Guid id, ModelProvider modelProvider, string modelName, string name, string roleMandate, string systemPrompt = StringHelper.EmptyString, AgentState state = null, bool disableProxy = false, bool enableThinking = false)
        {
            Id = id;
            Name = name;
            RoleMandate = roleMandate ?? string.Empty;
            State = state is null ? new AgentState(id, modelProvider.ProviderName, modelName) : state;

            try
            {
                NativeClient = NativeClientFactory.Create(modelProvider, modelName, $"{UtilPrompt.ContextPrompt}\r\n{systemPrompt}", enableThinking, disableProxy);
                NativeClient.OnToolCallsStart += async (toolCallParameters) => await (OnToolCallsStart is null ? Task.CompletedTask : OnToolCallsStart.Invoke(toolCallParameters));
                NativeClient.OnToolCallInvoke += async (toolCallParameter, toolCallResult) => await (OnToolCallInvoke is null ? Task.FromResult(toolCallResult) : OnToolCallInvoke.Invoke(toolCallParameter, toolCallResult));
                NativeClient.OnToolExecuting += async (functionName, toolCallParameter) => await (OnToolExecuting is null ? Task.CompletedTask : OnToolExecuting.Invoke(functionName, toolCallParameter));
                NativeClient.OnToolExecuted += async (functionName, toolCallParameter, toolCallResult) => await (OnToolExecuted is null ? Task.CompletedTask : OnToolExecuted.Invoke(functionName, toolCallParameter, toolCallResult));
                NativeClient.OnToolCallsFinish += async (toolCallParameters, toolCallResults) => await (OnToolCallsFinish is null ? Task.FromResult<ToolCallResult[]>(null) : OnToolCallsFinish.Invoke(toolCallParameters, toolCallResults));
                NativeClient.OnStreamOutput += async (chunk) => await (OnStreamOutput is null ? Task.CompletedTask : OnStreamOutput.Invoke(chunk));
                NativeClient.OnStreamOutputCompleted += NativeClient_OnStreamOutputCompleted;
            }
            catch (Exception)
            {
                // Handle the exception, log it, or rethrow it as needed
            }
        }

        private Task NativeClient_OnStreamOutputCompleted(Result result)
        {
            IsBusy = false;
            return OnStreamOutputCompleted?.Invoke(result) ?? Task.CompletedTask;
        }
        public async Task<Result> Chat(string input)
        {
            return await Task.Run(async () =>
            {
                if (input.IsNullOrEmpty()) return null;
                List<ChatCompletionsChunk> chunks = [];
                IsBusy = true;
                await foreach (var chunk in NativeClient.CompletionsStreamAsync(input, chunks))
                {

                }
                var result = Result.Create([.. chunks]);
                State.NativeHistory = [.. NativeClient.MessageHistory];
                State.LastAt = DateTime.Now;
                return result;
            });
        }
        public void RollbackHistory()
        {
            NativeClient.RollbackHistory();
            State.NativeHistory = [.. NativeClient.MessageHistory];
        }
        public void ClearHistory()
        {
            NativeClient.ClearHistory();
            State.NativeHistory = [.. NativeClient.MessageHistory];
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
            NativeClient?.Dispose();
            NativeClient = null;
        }
    }
}
