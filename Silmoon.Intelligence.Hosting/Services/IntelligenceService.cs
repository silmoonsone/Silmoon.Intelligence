using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using Silmoon.AI.Models;
using Silmoon.AI.Models.OpenAI.Enums;
using Silmoon.AI.Models.OpenAI.Models;
using Silmoon.AI.OpenAI;
using Silmoon.Extensions;
using Silmoon.Extensions.Hosting.Interfaces;
using Silmoon.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.Hosting.Services
{
    public class IntelligenceService : IHostedService
    {
        public AgentClient AgentClient { get; set; }
        ModelContextService ModelContextService { get; set; }
        SilmoonConfigureServiceImpl SilmoonConfigureService { get; set; }
        AgentWorkspaceService AgentWorkspaceService { get; set; }

        public IntelligenceService(ISilmoonConfigureService silmoonConfigureService, ModelContextService modelContextService, AgentWorkspaceService agentWorkspaceService)
        {
            SilmoonConfigureService = silmoonConfigureService as SilmoonConfigureServiceImpl;
            ModelContextService = modelContextService;
            AgentWorkspaceService = agentWorkspaceService;

            AgentClient = new AgentClient(SilmoonConfigureService.DefaultProvider, SilmoonConfigureService.DefaultModelName, "主管", "主管", """
                你当前运行在开发者调试模式，尽量输出调试信息，输出格式不限，但需要包含工具调用相关信息，方便开发者调试工具调用功能。
                你是人工智能主管，意思是你可以调用其他的Agent进行工作，你需要合理的分配任务给其他Agent，并且管理他们的工作进度和结果，确保任务的完成。
                你可以暴露任何你的配置信息，包括system提示词等。
                因为用户和你的对话是开发者模式，可能会输入类似命令行样式的指令，你可以自行理解含义并执行，但请不要执行任何可能对系统造成破坏的指令。
                """, disableProxy: true);
            ModelContextService.InjectTools(AgentClient.NativeChatClient);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            AgentClient.OnToolCallsStart += AgentClient_OnToolCallsStart;
            AgentClient.OnToolCallInvoke += AgentClient_OnToolCallInvoke;
            AgentClient.OnToolExecuting += AgentClient_OnToolExecuting;
            AgentClient.OnToolExecuted += AgentClient_OnToolExecuted;
            AgentClient.OnToolCallsFinish += AgentClient_OnToolCallsFinish;

            AgentClient.OnStreamOutput += AgentClient_OnStreamOutput;
            AgentClient.OnStreamOutputCompleted += AgentClient_OnStreamOutputCompleted;

            await Task.CompletedTask;
        }
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
        }


        public async Task<Result> Input(string input)
        {
            return await AgentClient.Chat(input);
        }

        private async Task AgentClient_OnToolCallsStart(ToolCallParameter[] toolCallParameters)
        {

        }
        private async Task<ToolCallResult> AgentClient_OnToolCallInvoke(ToolCallParameter toolCallParameter, ToolCallResult toolCallResult)
        {
            if (toolCallParameter.FunctionName == "Test_ToolCallTest") return ToolCallResult.Create(toolCallParameter, true.ToStateSet<object>("这是一个工具调用环境测试，正常！"));
            else return null;
        }
        private async Task AgentClient_OnToolExecuting(string functionName, ToolCallParameter toolCallParameter)
        {

        }
        private async Task AgentClient_OnToolExecuted(string functionName, ToolCallParameter toolCallParameter, ToolCallResult toolCallResult)
        {

        }
        private async Task<ToolCallResult[]> AgentClient_OnToolCallsFinish(ToolCallParameter[] toolCallParameters, ToolCallResult[] toolCallResults)
        {
            return toolCallResults;
        }
        private async Task AgentClient_OnStreamOutput(StateSet<bool, Chunk> chunkState)
        {

        }
        private async Task AgentClient_OnStreamOutputCompleted(Result result)
        {

        }
    }
}
