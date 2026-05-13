using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Silmoon.AI.Models;
using Silmoon.AI.Models.OpenAI.Enums;
using Silmoon.AI.Models.OpenAI.Models;
using Silmoon.AI.OpenAI;
using Silmoon.Extensions;
using Silmoon.Extensions.Hosting.Interfaces;
using Silmoon.Intelligence.Hosting.Tools;
using Silmoon.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.Hosting.Services
{
    public class IntelligenceService : BackgroundService
    {
        public AgentClient SupervisorAgentClient { get; set; }
        public AgentClient MainChatAgentClient { get; set; }
        ModelContextService ModelContextService { get; set; }
        SilmoonConfigureServiceImpl SilmoonConfigureService { get; set; }
        AgentWorkspaceService AgentWorkspaceService { get; set; }
        ILogger<IntelligenceService> Logger { get; set; }
        public ManualResetEvent ReadyResetEvent { get; set; } = new ManualResetEvent(false);

        public IntelligenceService(ISilmoonConfigureService silmoonConfigureService, ModelContextService modelContextService, AgentWorkspaceService agentWorkspaceService, ILogger<IntelligenceService> logger)
        {
            SilmoonConfigureService = silmoonConfigureService as SilmoonConfigureServiceImpl;
            ModelContextService = modelContextService;
            AgentWorkspaceService = agentWorkspaceService;
            Logger = logger;

            var supervisorAdditionSystemPrompt = File.ReadAllText(Path.Combine(agentWorkspaceService.WorkspaceDirectory, "system_prompts", "supervisor_agent_system.md"));
            var unifiedSystemPrompt = File.ReadAllText(Path.Combine(agentWorkspaceService.WorkspaceDirectory, "system_prompts", "unified_agent_system.md"));


            SupervisorAgentClient = new AgentClient(SilmoonConfigureService.DefaultProvider, SilmoonConfigureService.DefaultModelName, "Agent监管助手", "Agent监管员", $"""
                {supervisorAdditionSystemPrompt}
                {unifiedSystemPrompt}
                """, disableProxy: SilmoonConfigureService.NativeClientDisableProxy);

            MainChatAgentClient = new AgentClient(SilmoonConfigureService.DefaultProvider, SilmoonConfigureService.DefaultModelName, "主执行人", "主执行人", $"""
                你是人工智能执行人，意思是你可以调用其他的Agent进行工作，你需要合理的分配任务给其他Agent，并且管理他们的工作进度和结果，确保任务的完成。
                {unifiedSystemPrompt}
                """, disableProxy: SilmoonConfigureService.NativeClientDisableProxy);
        }

        public async override Task StartAsync(CancellationToken cancellationToken)
        {
            ModelContextService.InjectSupervisorTools(SupervisorAgentClient.NativeChatClient);
            ModelContextService.InjectMainChatTools(MainChatAgentClient.NativeChatClient);

            SupervisorAgentClient.OnToolCallsStart += SupervisorAgentClient_OnToolCallsStart;
            SupervisorAgentClient.OnToolCallInvoke += SupervisorAgentClient_OnToolCallInvoke;
            SupervisorAgentClient.OnToolExecuting += SupervisorAgentClient_OnToolExecuting;
            SupervisorAgentClient.OnToolExecuted += SupervisorAgentClient_OnToolExecuted;
            SupervisorAgentClient.OnToolCallsFinish += SupervisorAgentClient_OnToolCallsFinish;
            SupervisorAgentClient.OnStreamOutput += SupervisorAgentClient_OnStreamOutput;
            SupervisorAgentClient.OnStreamOutputCompleted += SupervisorAgentClient_OnStreamOutputCompleted;

            MainChatAgentClient.OnToolCallsStart += MainChatAgentClient_OnToolCallsStart;
            MainChatAgentClient.OnToolCallInvoke += MainChatAgentClient_OnToolCallInvoke;
            MainChatAgentClient.OnToolExecuting += MainChatAgentClient_OnToolExecuting;
            MainChatAgentClient.OnToolExecuted += MainChatAgentClient_OnToolExecuted;
            MainChatAgentClient.OnToolCallsFinish += MainChatAgentClient_OnToolCallsFinish;
            MainChatAgentClient.OnStreamOutput += MainChatAgentClient_OnStreamOutput;
            MainChatAgentClient.OnStreamOutputCompleted += MainChatAgentClient_OnStreamOutputCompleted;
            await base.StartAsync(cancellationToken);
        }

        public async Task<Result> Input(string input)
        {
            return await MainChatAgentClient.Chat($"<time>{DateTime.Now: yyyy-MM-dd HH:mm:ss}</time>{input}");
        }
        public StateSet<bool, JObject> SaveChatHistory()
        {
            return AgentWorkspaceService.SaveChatHistory(overwritten: true);
        }
        public StateSet<bool, JObject> RestoreChatHistory()
        {
            return AgentWorkspaceService.RestoreChatHistory();
        }

        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Logger.LogInformation("初始化监管Agent...");
            var result = await SupervisorAgentClient.Chat("系统已经启动，请恢复主聊天交互Agent状态");
            //Logger.LogInformation($"{result.Content}");
            Logger.LogInformation("监管Agent...完成");
            ReadyResetEvent.Set();
        }

        #region SupervisorAgentClient events
        private async Task SupervisorAgentClient_OnToolCallsStart(ToolCallParameter[] toolCallParameters)
        {

        }
        private async Task<ToolCallResult> SupervisorAgentClient_OnToolCallInvoke(ToolCallParameter toolCallParameter, ToolCallResult toolCallResult)
        {
            if (toolCallParameter.FunctionName == "Test_ToolCallTest") return ToolCallResult.Create(toolCallParameter, true.ToStateSet<object>("这是一个工具调用环境测试，正常！"));
            else return null;
        }
        private async Task SupervisorAgentClient_OnToolExecuting(string functionName, ToolCallParameter toolCallParameter)
        {

        }
        private async Task SupervisorAgentClient_OnToolExecuted(string functionName, ToolCallParameter toolCallParameter, ToolCallResult toolCallResult)
        {

        }
        private async Task<ToolCallResult[]> SupervisorAgentClient_OnToolCallsFinish(ToolCallParameter[] toolCallParameters, ToolCallResult[] toolCallResults)
        {
            return toolCallResults;
        }
        private async Task SupervisorAgentClient_OnStreamOutput(StateSet<bool, Chunk> chunkState)
        {

        }
        private async Task SupervisorAgentClient_OnStreamOutputCompleted(Result result)
        {

        }
        #endregion

        #region MainChatAgentClient events
        private async Task MainChatAgentClient_OnToolCallsStart(ToolCallParameter[] toolCallParameters)
        {

        }
        private async Task<ToolCallResult> MainChatAgentClient_OnToolCallInvoke(ToolCallParameter toolCallParameter, ToolCallResult toolCallResult)
        {
            if (toolCallParameter.FunctionName == "Test_ToolCallTest") return ToolCallResult.Create(toolCallParameter, true.ToStateSet<object>("这是一个工具调用环境测试，正常！"));
            else return null;
        }
        private async Task MainChatAgentClient_OnToolExecuting(string functionName, ToolCallParameter toolCallParameter)
        {

        }
        private async Task MainChatAgentClient_OnToolExecuted(string functionName, ToolCallParameter toolCallParameter, ToolCallResult toolCallResult)
        {

        }
        private async Task<ToolCallResult[]> MainChatAgentClient_OnToolCallsFinish(ToolCallParameter[] toolCallParameters, ToolCallResult[] toolCallResults)
        {
            return toolCallResults;
        }
        private async Task MainChatAgentClient_OnStreamOutput(StateSet<bool, Chunk> chunkState)
        {

        }
        private async Task MainChatAgentClient_OnStreamOutputCompleted(Result result)
        {

        }
        #endregion

        public override void Dispose()
        {
            MainChatAgentClient?.Dispose();
            SupervisorAgentClient?.Dispose();
        }
    }
}
