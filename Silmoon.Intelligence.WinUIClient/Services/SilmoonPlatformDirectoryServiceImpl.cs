using Silmoon.Extensions.Hosting.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Silmoon.Intelligence.WinUIClient.Services
{
    internal class SilmoonPlatformDirectoryServiceImpl : ISilmoonPlatformDirectoryService
    {
        public string AppConfigDirectory => Path.Combine(AppContext.BaseDirectory);

        public string AppDataDirectory => Path.Combine(AppContext.BaseDirectory);

        public string AppWorkingDirectory => Path.Combine(AppContext.BaseDirectory);
    }
}

