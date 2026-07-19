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

        public string Id { get => State.Id; set => State.Id = value; }
        public string Name { get; set; } = string.Empty;
        public string RoleMandate { get; set; } = string.Empty;
        public NativeMessageCollection History { get => NativeClient.MessageHistory; set => NativeClient.MessageHistory = value; }
        public bool IsBusy { get; set; } = false;
        public AgentState State { get; set; }

        [SetsRequiredMembers]
        public AgentClient(AgentState state, ModelProvider modelProvider, string modelName, string systemPrompt = StringHelper.EmptyString, bool disableProxy = false, bool enableThinking = false)
        {
            State = state;
            Name = $"agent-{GetShortId(Id)}";
            RoleMandate = $"agent";
            try
            {
                CreateNativeClient(modelProvider, modelName, systemPrompt, disableProxy, enableThinking);
            }
            catch (Exception)
            {
                // Handle the exception, log it, or rethrow it as needed
            }
        }
        [SetsRequiredMembers]
        public AgentClient(ModelProvider modelProvider, string modelName, string systemPrompt = StringHelper.EmptyString, bool disableProxy = false, bool enableThinking = false)
        {
            State = new AgentState(modelProvider.ProviderName, modelName);
            State.Topic = $"新对话({GetShortId(State.Id)})";
            Name = $"agent-{GetShortId(Id)}";
            RoleMandate = $"agent";
            try
            {
                CreateNativeClient(modelProvider, modelName, systemPrompt, disableProxy, enableThinking);
            }
            catch (Exception)
            {
                // Handle the exception, log it, or rethrow it as needed
            }
        }
        void CreateNativeClient(ModelProvider modelProvider, string modelName, string systemPrompt, bool disableProxy, bool enableThinking)
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
        static string GetShortId(string id)
        {
            if (id.IsNullOrEmpty()) return Guid.NewGuid().ToString("N")[..8];
            return id.Length <= 8 ? id : id[..8];
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
