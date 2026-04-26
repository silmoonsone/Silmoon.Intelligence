using Newtonsoft.Json.Linq;
using Silmoon.AI.Models;
using Silmoon.AI.Models.OpenAI.Enums;
using Silmoon.AI.Models.OpenAI.Models;
using Silmoon.AI.Tools;
using Silmoon.Extensions;
using Silmoon.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.Tools
{
    public class AgentModelTool : ExecuteTool
    {
        AgentModelManager AgentModelManager { get; set; }
        public AgentModelTool(AgentModelManager agentModelManager)
        {
            AgentModelManager = agentModelManager;
        }
        public override Tool[] GetTools()
        {
            return [
                Tool.Create("GetAgentModelProvidersTool", """
                Return configured providers and preset models for static agents.
                Call this before CallStaticAgentTool to get valid (providerName, modelName).
                Only returned pairs are callable.
                Prefer enabled models; use disabled models only if user explicitly requests that exact model.
                Keep apiKey-like fields secret.
                """,
                []),
                Tool.Create("CallStaticAgentTool", """
                Call a configured static agent once.
                providerName and modelName must exactly match GetAgentModelProvidersTool output.
                content is required; system is optional per-call override; reasonContent defaults to false.
                Use only when delegation to another configured model is needed.
                Return serialized Result on success; otherwise return failure message.
                """,
                [
                    new ToolParameterProperty("string", "providerName", "Provider id exactly as in config / GetAgentModelProvidersTool (used in client key providerName_modelName)."),
                    new ToolParameterProperty("string", "modelName", "Preset model name exactly as in that provider's models[] (must match client registration). Do not use a disabled model unless the user explicitly asked for that model."),
                    new ToolParameterProperty("string", "system", "Optional. System prompt for this call only; omit to keep host default."),
                    new ToolParameterProperty("string", "content", "User message or task to send to the target model."),
                    new ToolParameterProperty("bool", "reasonContent", "If true, enable thinking/reasoning on the client when the model supports it.", [true, false]),
                ]),
                Tool.Create("ResetStaticAgentHistoryTool","""
                Reset chat history of a static agent.
                providerName and modelName must exactly match a configured static agent.
                Use before a new independent task or when old context pollutes output.
                Do not use if continuing current context.
                Only history is cleared; configuration remains.
                """,
                [
                    new ToolParameterProperty("string", "providerName", "Provider id exactly as in config / GetAgentModelProvidersTool (used in client key providerName_modelName)."),
                    new ToolParameterProperty("string", "modelName", "Preset model name exactly as in that provider's models[] (must match client registration). Do not use a disabled model unless the user explicitly asked for that model."),
                ]),
            ];
        }

        public override async Task<List<ToolCallResult>> OnToolCallInvoke(ToolCallParameter[] toolCallParameters, Dictionary<string, ToolCallResult> toolCallResults)
        {
            List<ToolCallResult> results = [];
            foreach (var parameter in toolCallParameters)
            {
                var functionName = parameter.FunctionName;
                var parameters = parameter.Parameters;
                switch (functionName)
                {
                    case "GetAgentModelProvidersTool":
                        results.Add(ToolCallResult.Create(parameter, true.ToStateSet<string>(AgentModelManager.GetAgentModelProviders().ToJsonString())));
                        break;
                    case "CallStaticAgentTool":
                        var askResult = await AgentModelManager.CallStaticAgent(parameters["providerName"]?.Value<string>(), parameters["modelName"]?.Value<string>(), parameters["content"]?.Value<string>(), parameters["system"]?.Value<string>(), parameters["reasonContent"]?.Value<bool>() ?? false);
                        results.Add(ToolCallResult.Create(parameter, askResult.State.ToStateSet(askResult.Data?.ToJsonString(), askResult.Message)));
                        break;
                    case "ResetStaticAgentHistoryTool":
                        var resetResult = AgentModelManager.ResetStaticAgentHistory(parameters["providerName"]?.Value<string>(), parameters["modelName"]?.Value<string>());
                        results.Add(ToolCallResult.Create(parameter, resetResult.State.ToStateSet<string>(null, resetResult.Message)));
                        break;
                    default:
                        break;
                }
            }
            return results;
        }
    }
}
