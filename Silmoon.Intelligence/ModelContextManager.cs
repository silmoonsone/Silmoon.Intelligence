using Silmoon.AI.Interfaces;
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
        public void AddTools(INativeChatClient nativeChatClient, IExecuteTool[] executeTools)
        {
            nativeChatClient.ExecuteToolManager.AddExecuteTools(executeTools);
        }
    }
}



