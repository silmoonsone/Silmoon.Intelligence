using Newtonsoft.Json.Linq;
using Silmoon.AI.Models;
using Silmoon.AI.Models.OpenAI.Models;
using Silmoon.AI.Tools;
using Silmoon.Extensions;
using Silmoon.Extensions.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.Tools
{
    public class WebSearchTool : ExecuteTool
    {
        JsonRequestSetting JsonRequestSetting { get; set; }
        string AliyunOpenSearchKey { get; set; }
        public WebSearchTool(string aliyunOpenSearchKey)
        {
            AliyunOpenSearchKey = aliyunOpenSearchKey;
            JsonRequestSetting = new JsonRequestSetting();
            JsonRequestSetting.RequestHeaders.Add("Authorization", [$"Bearer {AliyunOpenSearchKey}"]);
        }
        public override Tool[] GetTools()
        {
            return [
                Tool.Create(
                    "WebSearch",
                    """
                    Search the web for information.
                    Concurrency: at most 3 WebSearch calls in parallel per tool-calls block.
                    If more searches are needed, run additional tool-calling rounds after results return instead of exceeding the limit.
                    """,
                    [
                        ToolParameterProperty.Create("string", "query", "The search query.", null, true)
                    ]
                )
                ];
        }

        public override async Task<ToolCallResult> OnToolCallInvoke(ToolCallParameter toolCallParameter, ToolCallResult toolCallResult)
        {
            var result = new ToolCallResult();
            var functionName = toolCallParameter.FunctionName;


            switch (functionName)
            {
                case "WebSearch":
                    if (!AliyunOpenSearchKey.IsNullOrEmpty())
                    {
                        var query = toolCallParameter.Parameters["query"].ToString();
                        var obj = new { query = query, query_rewrite = true, top_k = 10, content_type = "snippet" };
                        var postData = obj.ToJObject();
                        await NotifyToolExecuting("WebSearch", toolCallParameter);
                        var searchResult = await JsonRequest.PostAsync<JObject, JObject>("https://default-5w82.platform-cn-shanghai.opensearch.aliyuncs.com/v3/openapi/workspaces/default/web-search/ops-web-search-001", postData, null, JsonRequestSetting);
                        result = ToolCallResult.Create(toolCallParameter, searchResult.IsSuccessStatusCode.ToStateSet<object>(searchResult.Result, searchResult.Exception?.ToString()));
                        await NotifyToolExecuted("WebSearch", toolCallParameter, result);
                    }
                    else
                        return ToolCallResult.Create(toolCallParameter, false.ToStateSet<object>(null, "OpenSearch key is not configured."));
                    break;
                default:
                    break;
            }

            return result;
        }
    }
}
