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

namespace Silmoon.Intelligence.Core.Tools
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
                Return the configured agent-side model providers and their preset models (from the models[] list).

                When to call:
                - Before AskAgentModelTool, when you need valid (providerName, modelName) pairs or to verify what is registered for agent routing.

                How to use the result:
                - Each provider entry lists its preset models; only those names can be used with AskAgentModelTool.
                - Respect enable flags (e.g. enable: false): unless the user explicitly asks to use that specific model, do not call AskAgentModelTool for disabled models—prefer only enabled entries.
                - Treat any apiKey or similar fields as secrets: do not repeat them to end users unless necessary, and never paste them into unrelated channels.

                Notes:
                - This reflects the host configuration; if a combination is missing here, AskAgentModelTool will not be able to invoke it.
                """,
                []),
                Tool.Create("AskAgentModelTool", """
                Send a one-shot chat request to a specific preset model under a named provider.

                Required:
                - providerName and modelName must match the configuration exactly (same strings as in GetAgentModelProvidersTool / config). The host resolves clients as "{providerName}_{modelName}"; typos or extra spaces will fail.

                Parameters:
                - content: the user message or task for the target model.
                - system: optional. Overrides the client system prompt for this call only. Omit to keep the default configured behavior.
                - reasonContent: optional, default false. Enables extended reasoning/thinking on the client when the backend model supports it.

                When to call:
                - You need a different model or provider than the current assistant, and the pair exists in the preset list.

                When not to call:
                - For trivial work the current model can do without delegation.
                - If you have not yet confirmed providerName/modelName via GetAgentModelProvidersTool and config.
                - If the target model is disabled in configuration (e.g. enable: false), unless the user explicitly requested that exact model by name.

                Output:
                - Tool result is the serialized completion Result (including aggregated response); use it to answer the user, citing limitations if the call failed (e.g. model not found).
                """,
                [
                    new ToolParameterProperty("string", "providerName", "Provider id exactly as in config / GetAgentModelProvidersTool (used in client key providerName_modelName)."),
                    new ToolParameterProperty("string", "modelName", "Preset model name exactly as in that provider's models[] (must match client registration). Do not use a disabled model unless the user explicitly asked for that model."),
                    new ToolParameterProperty("string", "system", "Optional. System prompt for this call only; omit to keep host default."),
                    new ToolParameterProperty("string", "content", "User message or task to send to the target model."),
                    new ToolParameterProperty("bool", "reasonContent", "If true, enable thinking/reasoning on the client when the model supports it.", [true, false]),
                ])
            ];
        }

        public override async Task<StateSet<bool, string>> OnToolCallInvoke(string functionName, JObject parameters, string toolCallId, StateSet<bool, string> toolMessageState)
        {
            StateSet<bool, string> result = null;
            switch (functionName)
            {
                case "GetAgentModelProvidersTool":
                    result = true.ToStateSet<string>(AgentModelManager.GetAgentModelProvidersTool().ToJsonString());
                    break;
                case "AskAgentModelTool":
                    var askResult = await AgentModelManager.CallAgentModel(parameters["providerName"]?.Value<string>(), parameters["modelName"]?.Value<string>(), parameters["content"]?.Value<string>(), parameters["system"]?.Value<string>(), parameters["reasonContent"]?.Value<bool>() ?? false);
                    result = true.ToStateSet<string>(askResult.ToJsonString());
                    break;
                default:
                    break;
            }
            return await Task.FromResult(result);
        }
    }
}
