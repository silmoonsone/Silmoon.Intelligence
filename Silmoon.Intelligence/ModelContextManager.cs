using Silmoon.AI.Interfaces;
using Silmoon.AI.OpenAI;
using System;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence
{
    public class ModelContextManager
    {
        public ModelContextManager()
        {

        }
        public void AddTools(NativeChatClient nativeChatClient, IExecuteTool[] executeTools)
        {
            nativeChatClient.ExecuteToolManager.AddExecuteTools(executeTools);
        }
    }
}
