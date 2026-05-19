using Silmoon.AI.Models;
using Silmoon.AI.Models.OpenAI.Models;
using Silmoon.AI.Tools;
using Silmoon.Extensions;
using Silmoon.Intelligence.Hosting.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.Hosting.Tools
{
    public class AgentStateTool : ExecuteTool
    {
        IntelligenceService IntelligenceService { get; set; }
        public AgentStateTool(IntelligenceService intelligenceService)
        {
            IntelligenceService = intelligenceService;
        }
        public override Tool[] GetTools()
        {
            return [
                //Tool.Create("MainAgent_StateControl", "操作主聊天交互Agent生命周期。",
                //[
                //    new ToolParameterProperty("string", "action", "一个操作，目前支持restore（恢复）、save（保存）。", ["restore", "save"], true),
                //    new ToolParameterProperty("string", "param", "附带的参数，目前是占位符，可以null即可，或者留空注意JSON格式合格。", null, false)
                //]),
                ];
        }

        public override async Task<ToolCallResult> OnToolCallInvoke(ToolCallParameter toolCallParameter, ToolCallResult toolCallResult)
        {
            var functionName = toolCallParameter.FunctionName;
            var parameters = toolCallParameter.Parameters;
            ToolCallResult result = null;
            //switch (functionName)
            //{
            //    case "MainAgent_StateControl":
            //        var str = parameters.ToJsonString();
            //        var action = parameters?.Value<string>("action");
            //        switch (action)
            //        {
            //            case "save":
            //                await NotifyToolExecuting(functionName, toolCallParameter);
            //                var saveResult = IntelligenceService.SaveChatHistory();
            //                result = ToolCallResult.Create(toolCallParameter, saveResult.State.ToStateSet<object>(saveResult.Data, saveResult.Message));
            //                await NotifyToolExecuted(functionName, toolCallParameter, result);
            //                break;
            //            case "restore":
            //                await NotifyToolExecuting(functionName, toolCallParameter);
            //                var restoreResult = IntelligenceService.RestoreChatHistory();
            //                result = ToolCallResult.Create(toolCallParameter, restoreResult.State.ToStateSet<object>(restoreResult.Data, restoreResult.Message));
            //                await NotifyToolExecuted(functionName, toolCallParameter, result);
            //                break;
            //            default:
            //                break;
            //        }
            //        break;
            //    default:
            //        break;
            //}
            return result;
        }
    }
}
