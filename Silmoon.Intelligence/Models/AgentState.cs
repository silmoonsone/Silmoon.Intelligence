using Silmoon.AI.OpenAI.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.Models
{
    public class AgentState
    {
        public Guid Id { get; set; }
        public string ProviderName { get; set; }
        public string ModelName { get; set; }
        public string Topic { get; set; } = string.Empty;
        public INativeMessage[] NativeHistory { get; set; } = [];
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastAt { get; set; } = DateTime.Now;

        public AgentState(Guid id, string providerName, string modelName)
        {
            Id = id;
            ProviderName = providerName;
            ModelName = modelName;
        }
    }
}

