using Newtonsoft.Json;
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
    public class ModelTool : ExecuteTool
    {
        ModelsManager ModelsManager { get; set; }
        public ModelTool(ModelsManager modelsManager)
        {
            ModelsManager = modelsManager;
        }
        public override Tool[] GetTools()
        {
            return [
                Tool.Create("GetModelsTool", """
                Get a list of all available large language models
                """,
                []),
                Tool.Create("AskModelTool", """
                Ask or chat with a specific model by name. You can specify the system role, format, and language in the system parameter. If you omit the system parameter, the model will use the default settings.
                """,
                [
                    new ToolParameterProperty("string", "modelName", "Call a specific model by name."),
                    new ToolParameterProperty("string", "content", "Chat or ask content"),
                    new ToolParameterProperty("string", "system", "Optional. Role, format, language. Omit to keep default."),
                ])
            ];
        }

        public override async Task<StateSet<bool, MessageContent>> OnToolCallInvoke(string functionName, JObject parameters, string toolCallId, StateSet<bool, MessageContent> toolMessageState)
        {
            StateSet<bool, MessageContent> result = null;
            switch (functionName)
            {
                case "GetModelsTool":
                    Dictionary<string, ModelConfig> models = [];
                    foreach (var item in ModelsManager.Models)
                    {
                        var model = JsonConvert.DeserializeObject<KeyValuePair<string, ModelConfig>>(item.ToJsonString());
                        model.Value.ApiKey = "hidden";
                        models.Add(model.Key, model.Value);
                    }
                    result = true.ToStateSet(MessageContent.Create(Role.Tool, models.ToJsonString(), toolCallId));
                    break;
                case "AskModelTool":
                    var askResult = await ModelsManager.Ask(parameters["modelName"].ToString(), parameters["content"].ToString(), parameters["system"]?.ToString());
                    result = true.ToStateSet(MessageContent.Create(Role.Tool, askResult.ToJsonString(), toolCallId));
                    break;
                default:
                    break;
            }
            return await Task.FromResult(result);
        }
    }
}
