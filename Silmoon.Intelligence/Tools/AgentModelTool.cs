using Newtonsoft.Json.Linq;
using Silmoon.AI.Models;
using Silmoon.AI.Models.OpenAI.Enums;
using Silmoon.AI.Models.OpenAI.Models;
using Silmoon.AI.Tools;
using Silmoon.Extensions;
using Silmoon.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.Tools
{
    public class AgentModelTool : ExecuteTool
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
                List callable singleton agents.
                Run first. Only returned (providerName, modelName) pairs are valid.
                Each pair maps to one shared singleton agent instance.
                Prefer enabled models; use disabled ones only if user explicitly requests that exact model.
                Never reveal apiKey-like fields.
                """,
                []),
                Tool.Create(CallSingletonAgentFunctionName, $"""
                Delegate one task to a singleton agent.
                Target is selected by (providerName, modelName) and keeps history across calls.
                providerName/modelName must exactly match {GetAgentModelProvidersFunctionName} output.
                content is required; system is optional; reasonContent defaults to false.
                Concurrency:
                - Same pair: no parallel calls (must be serial).
                - Different pairs: parallel calls are allowed and recommended.
                Returns serialized Result, or failure message.
                """,
                [
                    new ToolParameterProperty("string", "providerName", $"Provider id exactly as in config / {GetAgentModelProvidersFunctionName} (used in client key providerName_modelName)."),
                    new ToolParameterProperty("string", "modelName", "Preset model name exactly as in that provider's models[] (must match client registration). Do not use a disabled model unless the user explicitly asked for that model."),
                    new ToolParameterProperty("string", "system", "Optional. System prompt for this call only; omit to keep host default."),
                    new ToolParameterProperty("string", "content", "User message or task to send to the target model."),
                    new ToolParameterProperty("boolean", "reasonContent", "If true, enable thinking/reasoning on the client when the model supports it.", [true, false]),
                ]),
                Tool.Create(ResetSingletonAgentHistoryFunctionName,"""
                Reset history for one singleton agent pair (providerName, modelName).
                Use before a new independent task or when old context pollutes output.
                Do not reset when continuity is needed.
                Only history is cleared; configuration is unchanged.
                """,
                [
                    new ToolParameterProperty("string", "providerName", $"Provider id exactly as in config / {GetAgentModelProvidersFunctionName} (used in client key providerName_modelName)."),
                    new ToolParameterProperty("string", "modelName", "Preset model name exactly as in that provider's models[] (must match client registration). Do not use a disabled model unless the user explicitly asked for that model."),
                ]),
                Tool.Create(GetWorkerAgentsFunctionName, """
                List all registered worker agents (digital employees).
                Use this first when you need valid worker names.
                """,
                []),
                Tool.Create(GetWorkerAgentFunctionName, $"""
                Get one worker agent by name.
                Use after {GetWorkerAgentsFunctionName} when details for a single worker are needed.
                name is required.
                """,
                [
                    new ToolParameterProperty("string", "name", "Worker agent unique name (digital employee name)."),
                ]),
                Tool.Create(CreateWorkerAgentFunctionName, $"""
                Create a worker agent (digital employee).
                Required: providerName, modelName, name.
                Recommended flow:
                1) Call {GetAgentModelProvidersFunctionName} to get valid provider/model pairs.
                2) Create with a unique name, roleMandate (identity + duties), and optional systemPrompt.
                Concurrency:
                - Same worker name: do NOT create in parallel.
                - Different names: parallel creation is allowed.
                """,
                [
                    new ToolParameterProperty("string", "providerName", $"Provider id from {GetAgentModelProvidersFunctionName}."),
                    new ToolParameterProperty("string", "modelName", "Model name from that provider."),
                    new ToolParameterProperty("string", "name", "Unique worker agent name (digital employee name)."),
                    new ToolParameterProperty("string", "roleMandate", "Optional. Who this worker is, what they do, and scope of responsibility."),
                    new ToolParameterProperty("string", "systemPrompt", "Optional. Extra system instructions for this worker; combined with host context at creation."),
                ]),
                Tool.Create(CallWorkerAgentFunctionName, """
                Send one task/message to a worker agent by name.
                name and content are required.
                Concurrency:
                - Same worker name: run serially to avoid context races.
                - Different worker names: parallel calls are allowed and recommended.
                Returns serialized Result, or failure message.
                """,
                [
                    new ToolParameterProperty("string", "name", "Worker agent unique name."),
                    new ToolParameterProperty("string", "content", "Task/message for the worker agent."),
                ]),
                Tool.Create(ResetWorkerAgentHistoryFunctionName, """
                Reset one worker agent's chat history by name.
                Use before a new independent task; do not reset if continuity is needed.
                """,
                [
                    new ToolParameterProperty("string", "name", "Worker agent unique name."),
                ]),
                Tool.Create(RemoveWorkerAgentFunctionName, """
                Remove one worker agent from manager by name.
                Use when the worker is no longer needed.
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
                    result = ToolCallResult.Create(toolCallParameter, true.ToStateSet<string>(AgentModelManager.GetAgentModelProviders().ToJsonString()));
                    await NotifyToolExecuted(functionName, toolCallParameter, result);
                    break;
                case CallSingletonAgentFunctionName:
                    await NotifyToolExecuting(functionName, toolCallParameter);
                    var askResult = await AgentModelManager.CallSingletonAgent(parameters["providerName"]?.Value<string>(), parameters["modelName"]?.Value<string>(), parameters["content"]?.Value<string>(), parameters["system"]?.Value<string>(), parameters["reasonContent"]?.Value<bool>() ?? false);
                    result = ToolCallResult.Create(toolCallParameter, askResult.State.ToStateSet(askResult.Data?.ToJsonString(), askResult.Message));
                    await NotifyToolExecuted(functionName, toolCallParameter, result);
                    break;
                case ResetSingletonAgentHistoryFunctionName:
                    await NotifyToolExecuting(functionName, toolCallParameter);
                    var resetResult = AgentModelManager.ResetSingletonAgentHistory(parameters["providerName"]?.Value<string>(), parameters["modelName"]?.Value<string>());
                    result = ToolCallResult.Create(toolCallParameter, resetResult.State.ToStateSet<string>(null, resetResult.Message));
                    await NotifyToolExecuted(functionName, toolCallParameter, result);
                    break;
                case GetWorkerAgentsFunctionName:
                    await NotifyToolExecuting(functionName, toolCallParameter);
                    result = ToolCallResult.Create(toolCallParameter, true.ToStateSet<string>(AgentModelManager.GetWorkerAgentClients().ToJsonString()));
                    await NotifyToolExecuted(functionName, toolCallParameter, result);
                    break;
                case GetWorkerAgentFunctionName:
                    await NotifyToolExecuting(functionName, toolCallParameter);
                    var workerClient = AgentModelManager.GetWorkerAgentClient(parameters["name"]?.Value<string>());
                    if (workerClient is not null)
                        result = ToolCallResult.Create(toolCallParameter, true.ToStateSet<string>(workerClient.ToJsonString()));
                    else
                        result = ToolCallResult.Create(toolCallParameter, false.ToStateSet<string>(null, $"specified worker agent ({parameters["name"]?.Value<string>()}) not found"));
                    break;
                case CreateWorkerAgentFunctionName:
                    await NotifyToolExecuting(functionName, toolCallParameter);
                    var createResult = AgentModelManager.CreateWorkerAgent(
                        parameters["providerName"]?.Value<string>(),
                        parameters["modelName"]?.Value<string>(),
                        parameters["name"]?.Value<string>(),
                        parameters["roleMandate"]?.Value<string>(),
                        parameters["systemPrompt"]?.Value<string>());
                    result = ToolCallResult.Create(toolCallParameter, createResult.State.ToStateSet(createResult.Data?.ToJsonString(), createResult.Message));
                    await NotifyToolExecuted(functionName, toolCallParameter, result);
                    break;
                case CallWorkerAgentFunctionName:
                    await NotifyToolExecuting(functionName, toolCallParameter);
                    var callWorkerResult = await AgentModelManager.CallWorkerAgent(
                        parameters["name"]?.Value<string>(),
                        parameters["content"]?.Value<string>());
                    result = ToolCallResult.Create(toolCallParameter, callWorkerResult.State.ToStateSet(callWorkerResult.Data?.ToJsonString(), callWorkerResult.Message));
                    await NotifyToolExecuted(functionName, toolCallParameter, result);
                    break;
                case ResetWorkerAgentHistoryFunctionName:
                    await NotifyToolExecuting(functionName, toolCallParameter);
                    var resetWorkerResult = AgentModelManager.ResetWorkerAgentHistory(parameters["name"]?.Value<string>());
                    result = ToolCallResult.Create(toolCallParameter, resetWorkerResult.State.ToStateSet<string>(null, resetWorkerResult.Message));
                    await NotifyToolExecuted(functionName, toolCallParameter, result);
                    break;
                case RemoveWorkerAgentFunctionName:
                    await NotifyToolExecuting(functionName, toolCallParameter);
                    var removeWorkerResult = AgentModelManager.RemoveWorkerAgent(parameters["name"]?.Value<string>());
                    result = ToolCallResult.Create(toolCallParameter, removeWorkerResult.State.ToStateSet<string>(null, removeWorkerResult.Message));
                    await NotifyToolExecuted(functionName, toolCallParameter, result);
                    break;
                default:
                    break;
            }
            return result;
        }
    }
}
