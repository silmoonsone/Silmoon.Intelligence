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
        AgentModelManager AgentModelManager { get; set; }
        public AgentModelTool(AgentModelManager agentModelManager)
        {
            AgentModelManager = agentModelManager;
        }
        public override Tool[] GetTools()
        {
            return [
                Tool.Create("GetAgentModelProvidersTool", """
                List callable singleton agents.
                Run first. Only returned (providerName, modelName) pairs are valid.
                Each pair maps to one shared singleton agent instance.
                Prefer enabled models; use disabled ones only if user explicitly requests that exact model.
                Never reveal apiKey-like fields.
                """,
                []),
                Tool.Create("CallSingletonAgentTool", """
                Delegate one task to a singleton agent.
                Target is selected by (providerName, modelName) and keeps history across calls.
                providerName/modelName must exactly match GetAgentModelProvidersTool output.
                content is required; system is optional; reasonContent defaults to false.
                Concurrency:
                - Same pair: no parallel calls (must be serial).
                - Different pairs: parallel calls are allowed and recommended.
                Returns serialized Result, or failure message.
                """,
                [
                    new ToolParameterProperty("string", "providerName", "Provider id exactly as in config / GetAgentModelProvidersTool (used in client key providerName_modelName)."),
                    new ToolParameterProperty("string", "modelName", "Preset model name exactly as in that provider's models[] (must match client registration). Do not use a disabled model unless the user explicitly asked for that model."),
                    new ToolParameterProperty("string", "system", "Optional. System prompt for this call only; omit to keep host default."),
                    new ToolParameterProperty("string", "content", "User message or task to send to the target model."),
                    new ToolParameterProperty("bool", "reasonContent", "If true, enable thinking/reasoning on the client when the model supports it.", [true, false]),
                ]),
                Tool.Create("ResetSingletonAgentHistoryTool","""
                Reset history for one singleton agent pair (providerName, modelName).
                Use before a new independent task or when old context pollutes output.
                Do not reset when continuity is needed.
                Only history is cleared; configuration is unchanged.
                """,
                [
                    new ToolParameterProperty("string", "providerName", "Provider id exactly as in config / GetAgentModelProvidersTool (used in client key providerName_modelName)."),
                    new ToolParameterProperty("string", "modelName", "Preset model name exactly as in that provider's models[] (must match client registration). Do not use a disabled model unless the user explicitly asked for that model."),
                ]),
            ];
        }

        public override async Task<List<ToolCallResult>> OnToolCallInvoke(ToolCallParameter[] toolCallParameters, ConcurrentDictionary<string, ToolCallResult> toolCallResults)
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
                    case "CallSingletonAgentTool":
                        var askResult = await AgentModelManager.CallSingletonAgent(parameters["providerName"]?.Value<string>(), parameters["modelName"]?.Value<string>(), parameters["content"]?.Value<string>(), parameters["system"]?.Value<string>(), parameters["reasonContent"]?.Value<bool>() ?? false);
                        results.Add(ToolCallResult.Create(parameter, askResult.State.ToStateSet(askResult.Data?.ToJsonString(), askResult.Message)));
                        break;
                    case "ResetSingletonAgentHistoryTool":
                        var resetResult = AgentModelManager.ResetSingletonAgentHistory(parameters["providerName"]?.Value<string>(), parameters["modelName"]?.Value<string>());
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
