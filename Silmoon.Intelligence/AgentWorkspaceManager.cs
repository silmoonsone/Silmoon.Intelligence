using Silmoon.AI.OpenAI.Models;
using System;
using System.Collections.Generic;
using System.Text;
using Silmoon.Extensions;
using Newtonsoft.Json.Linq;

namespace Silmoon.Intelligence
{
    public class AgentWorkspaceManager
    {
        public string WorkspaceDirectory { get; private set; }
        public string MainAgentMemoryDirectory { get; set; }
        public string MainAgentChatHistoryFile { get; set; }
        public List<MessageContent> MainAgentChatHistory { get; set; } = [];
        public AgentWorkspaceManager(string workspaceDirectory = "workspace")
        {
            WorkspaceDirectory = Path.Combine(AppContext.BaseDirectory, workspaceDirectory);
            Directory.CreateDirectoryRecursive(WorkspaceDirectory);

            MainAgentMemoryDirectory = Path.Combine(WorkspaceDirectory, "main_agent_memories");
            Directory.CreateDirectoryRecursive(MainAgentMemoryDirectory);

            MainAgentChatHistoryFile = Path.Combine(MainAgentMemoryDirectory, "main_agent_chat_history.json");
            if (File.Exists(MainAgentChatHistoryFile)) LoadMainAgentChatHistory();
        }
        public List<MessageContent> LoadMainAgentChatHistory()
        {
            if (File.Exists(MainAgentChatHistoryFile))
            {
                var jsons = JsonHelperV2.LoadJsonFromFile<JArray>(MainAgentChatHistoryFile);
                MainAgentChatHistory = [.. jsons.ToObjects<MessageContent>() ?? []];
                return MainAgentChatHistory;
            }
            else return [];
        }
        public List<MessageContent> LoadAgentChatHistory(string agentName)
        {
            var chatHistoryPath = Path.Combine(MainAgentMemoryDirectory, $"{agentName}_chat_history.json");
            if (File.Exists(chatHistoryPath))
            {
                var jsons = JsonHelperV2.LoadJsonFromFile<JArray>(chatHistoryPath);
                List<MessageContent> chatHistory = [.. jsons.ToObjects<MessageContent>() ?? []];
                return chatHistory;
            }
            else return [];
        }
    }
}

