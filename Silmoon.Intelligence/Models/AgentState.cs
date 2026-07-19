using Silmoon.AI.OpenAI.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.Models
{
    public class AgentState
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string ProviderName { get; set; }
        public string ModelName { get; set; }
        public string Topic { get; set; } = string.Empty;
        public INativeMessage[] NativeHistory { get; set; } = [];
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastAt { get; set; } = DateTime.Now;


        public AgentState(string providerName, string modelName)
        {
            ProviderName = providerName;
            ModelName = modelName;
        }
        public AgentState() { }
    }
}

