using Silmoon.Extensions.Hosting.Interfaces;

namespace Silmoon.Intelligence.Client.Services
{
    internal class SilmoonPlatformDirectoryServiceImpl : ISilmoonPlatformDirectoryService
    {
        public string AppConfigDirectory => AppContext.BaseDirectory;

        public string AppDataDirectory => AppContext.BaseDirectory;

        public string AppWorkingDirectory => AppContext.BaseDirectory;
    }
}
