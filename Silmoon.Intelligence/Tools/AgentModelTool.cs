using Newtonsoft.Json.Linq;
using Silmoon.AI.Models;
using Silmoon.AI.OpenAI.Models.Enums;
using Silmoon.AI.OpenAI.Models;
using Silmoon.AI.Tools;
using Silmoon.Extensions;
using Silmoon.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.Tools
{
    public class AgentModelTool : ToolSet
    {
        public const string GetAgentModelProvidersFunctionName = "AgentModel_GetAgentModelProviders";
        public const string CallSingletonAgentFunctionName = "AgentModel_CallSingletonAgent";
        public const string ResetSingletonAgentHistoryFunctionName = "AgentModel_ResetSingletonAgentHistory";
        public const string GetWorkerAgentsFunctionName = "AgentModel_GetWorkerAgents";
        public const string GetWorkerAgentFunctionName = "AgentModel_GetWorkerAgent";
        public const string CreateWorkerAgentFunctionName = "AgentModel_CreateWorkerAgent";
        public const string CallWorkerAgentFunctionName = "AgentModel_CallWorkerAgent";
        public const string ResetWorkerAgentHistoryFunctionName = "AgentModel_ResetWorkerAgentHistory";
        public const string RemoveWorkerAgentFunctionName = "AgentModel_RemoveWorkerAgent";

        AgentModelManager AgentModelManager { get; set; }
        public AgentModelTool(AgentModelManager agentModelManager)
        {
            AgentModelManager = agentModelManager;
        }
        public override Tool[] GetTools()
        {
            return [
                Tool.Create(GetAgentModelProvidersFunctionName, """
                List valid singleton agent model pairs.
                Call first. Only returned (providerName, modelName) are valid.
                Matching is case-sensitive.
                Return JSON object with `State`, `Message`, `Data` (`Data` is provider-model list JSON string).
                Never reveal apiKey-like fields.
                """,
                []),
                Tool.Create(CallSingletonAgentFunctionName, $"""
                Call one singleton agent (stateful per providerName+modelName).
                providerName/modelName must exactly match {GetAgentModelProvidersFunctionName} output (case-sensitive).
                Required: content. Optional: system. reasonContent default: false.
                Concurrency: same pair serial; different pairs can run in parallel.
                Return JSON object with `State`, `Message`, `Data` (`Data` is delegated result JSON string).
                """,
                [
                    new ToolParameterProperty("string", "providerName", $"Provider name from {GetAgentModelProvidersFunctionName}, exact and case-sensitive."),
                    new ToolParameterProperty("string", "modelName", "Model name from that provider, exact and case-sensitive."),
                    new ToolParameterProperty("string", "system", "Optional. System prompt for this call only; omit to keep host default."),
                    new ToolParameterProperty("string", "content", "User message or task to send to the target model."),
                    new ToolParameterProperty("boolean", "reasonContent", "If true, enable thinking/reasoning on the client when the model supports it.", [true, false]),
                ]),
                Tool.Create(ResetSingletonAgentHistoryFunctionName,"""
                Reset one singleton agent history by providerName+modelName.
                Use before a new independent task.
                Only history is cleared.
                Return JSON object with `State`, `Message`, `Data` (`Data` may be null).
                """,
                [
                    new ToolParameterProperty("string", "providerName", $"Provider name from {GetAgentModelProvidersFunctionName}, exact and case-sensitive."),
                    new ToolParameterProperty("string", "modelName", "Model name from that provider, exact and case-sensitive."),
                ]),
                Tool.Create(GetWorkerAgentsFunctionName, """
                List all registered worker agents.
                Call first when you need valid worker names.
                Return JSON object with `State`, `Message`, `Data` (`Data` is worker list JSON string).
                """,
                []),
                Tool.Create(GetWorkerAgentFunctionName, $"""
                Get one worker agent by name.
                Call after {GetWorkerAgentsFunctionName}.
                Required: name (case-sensitive).
                Return JSON object with `State`, `Message`, `Data` (`Data` is worker JSON string).
                """,
                [
                    new ToolParameterProperty("string", "name", "Worker agent unique name (digital employee name)."),
                ]),
                Tool.Create(CreateWorkerAgentFunctionName, $"""
                Create one worker agent.
                Required: providerName, modelName, name.
                providerName/modelName must come from {GetAgentModelProvidersFunctionName} (case-sensitive).
                name must be unique (case-sensitive).
                Same name must not be created in parallel.
                Return JSON object with `State`, `Message`, `Data` (`Data` is created worker JSON string).
                """,
                [
                    new ToolParameterProperty("string", "providerName", $"Provider name from {GetAgentModelProvidersFunctionName}, exact and case-sensitive."),
                    new ToolParameterProperty("string", "modelName", "Model name from that provider, exact and case-sensitive."),
                    new ToolParameterProperty("string", "name", "Unique worker agent name (digital employee name)."),
                    new ToolParameterProperty("string", "roleMandate", "Optional. Who this worker is, what they do, and scope of responsibility."),
                    new ToolParameterProperty("string", "systemPrompt", "Optional. Extra system instructions for this worker; combined with host context at creation."),
                ]),
                Tool.Create(CallWorkerAgentFunctionName, """
                Send one task/message to a worker agent by name.
                Required: name, content.
                name is case-sensitive.
                Concurrency: same name serial; different names can run in parallel.
                Return JSON object with `State`, `Message`, `Data` (`Data` is worker result JSON string).
                """,
                [
                    new ToolParameterProperty("string", "name", "Worker agent unique name."),
                    new ToolParameterProperty("string", "content", "Task/message for the worker agent."),
                ]),
                Tool.Create(ResetWorkerAgentHistoryFunctionName, """
                Reset one worker agent's chat history by name.
                name is case-sensitive.
                Return JSON object with `State`, `Message`, `Data` (`Data` may be null).
                """,
                [
                    new ToolParameterProperty("string", "name", "Worker agent unique name."),
                ]),
                Tool.Create(RemoveWorkerAgentFunctionName, """
                Remove one worker agent from manager by name.
                name is case-sensitive.
                Return JSON object with `State`, `Message`, `Data` (`Data` may be null).
                """,
                [
                    new ToolParameterProperty("string", "name", "Worker agent unique name."),
                ]),
            ];
        }

        public override async Task<ToolCallResult> OnToolCallInvoke(ToolCallParameter toolCallParameter, ToolCallResult toolCallResults)
        {
            ToolCallResult result = null;

            var functionName = toolCallParameter.FunctionName;
            var parameters = toolCallParameter.Parameters;
            switch (functionName)
            {
                case GetAgentModelProvidersFunctionName:
                    await NotifyToolExecuting(functionName, toolCallParameter);
                    result = ToolCallResult.Create(toolCallParameter, true.ToStateSet<object>(AgentModelManager.GetAgentModelProviders()));
                    await NotifyToolExecuted(functionName, toolCallParameter, result);
                    break;
                case CallSingletonAgentFunctionName:
                    await NotifyToolExecuting(functionName, toolCallParameter);
                    var askResult = await AgentModelManager.CallSingletonAgent(parameters["providerName"]?.Value<string>(), parameters["modelName"]?.Value<string>(), parameters["content"]?.Value<string>(), parameters["system"]?.Value<string>(), parameters["reasonContent"]?.Value<bool>() ?? false);
                    result = ToolCallResult.Create(toolCallParameter, askResult.State.ToStateSet((object)askResult.Data, askResult.Message));
                    await NotifyToolExecuted(functionName, toolCallParameter, result);
                    break;
                case ResetSingletonAgentHistoryFunctionName:
                    await NotifyToolExecuting(functionName, toolCallParameter);
                    var resetResult = AgentModelManager.ResetSingletonAgentHistory(parameters["providerName"]?.Value<string>(), parameters["modelName"]?.Value<string>());
                    result = ToolCallResult.Create(toolCallParameter, resetResult.State.ToStateSet<object>(null, resetResult.Message));
                    await NotifyToolExecuted(functionName, toolCallParameter, result);
                    break;
                case GetWorkerAgentsFunctionName:
                    await NotifyToolExecuting(functionName, toolCallParameter);
                    result = ToolCallResult.Create(toolCallParameter, true.ToStateSet<object>(AgentModelManager.GetWorkerAgentClients()));
                    await NotifyToolExecuted(functionName, toolCallParameter, result);
                    break;
                case GetWorkerAgentFunctionName:
                    await NotifyToolExecuting(functionName, toolCallParameter);
                    var workerClient = AgentModelManager.GetWorkerAgentClient(parameters["name"]?.Value<string>());
                    if (workerClient is not null)
                        result = ToolCallResult.Create(toolCallParameter, true.ToStateSet<object>(workerClient));
                    else
                        result = ToolCallResult.Create(toolCallParameter, false.ToStateSet<object>(null, $"specified worker agent ({parameters["name"]?.Value<string>()}) not found"));
                    await NotifyToolExecuted(functionName, toolCallParameter, result);
                    break;
                case CreateWorkerAgentFunctionName:
                    await NotifyToolExecuting(functionName, toolCallParameter);
                    var createResult = AgentModelManager.CreateWorkerAgent(
                        parameters["providerName"]?.Value<string>(),
                        parameters["modelName"]?.Value<string>(),
                        parameters["name"]?.Value<string>(),
                        parameters["roleMandate"]?.Value<string>(),
                        parameters["systemPrompt"]?.Value<string>());
                    result = ToolCallResult.Create(toolCallParameter, createResult.State.ToStateSet((object)createResult.Data, createResult.Message));
                    await NotifyToolExecuted(functionName, toolCallParameter, result);
                    break;
                case CallWorkerAgentFunctionName:
                    await NotifyToolExecuting(functionName, toolCallParameter);
                    var callWorkerResult = await AgentModelManager.CallWorkerAgent(
                        parameters["name"]?.Value<string>(),
                        parameters["content"]?.Value<string>());
                    result = ToolCallResult.Create(toolCallParameter, callWorkerResult.State.ToStateSet((object)callWorkerResult.Data, callWorkerResult.Message));
                    await NotifyToolExecuted(functionName, toolCallParameter, result);
                    break;
                case ResetWorkerAgentHistoryFunctionName:
                    await NotifyToolExecuting(functionName, toolCallParameter);
                    var resetWorkerResult = AgentModelManager.ResetWorkerAgentHistory(parameters["name"]?.Value<string>());
                    result = ToolCallResult.Create(toolCallParameter, resetWorkerResult.State.ToStateSet((object)null, resetWorkerResult.Message));
                    await NotifyToolExecuted(functionName, toolCallParameter, result);
                    break;
                case RemoveWorkerAgentFunctionName:
                    await NotifyToolExecuting(functionName, toolCallParameter);
                    var removeWorkerResult = AgentModelManager.RemoveWorkerAgent(parameters["name"]?.Value<string>());
                    result = ToolCallResult.Create(toolCallParameter, removeWorkerResult.State.ToStateSet<object>(null, removeWorkerResult.Message));
                    await NotifyToolExecuted(functionName, toolCallParameter, result);
                    break;
                default:
                    break;
            }
            return result;
        }
    }
}

