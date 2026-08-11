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
        public void AddTools(INativeClient nativeClient, IToolSet[] executeTools)
        {
            nativeClient.ToolSetManager.AddToolSets(executeTools);
        }
    }
}
