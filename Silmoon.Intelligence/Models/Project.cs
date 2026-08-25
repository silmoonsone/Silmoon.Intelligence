using System;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.Models
{
    public class Project
    {
        public required string Id { get; set; } = string.Empty;
        public string Name { get; set; }
        public List<string> Agents { get; set; } = [];
    }
}
