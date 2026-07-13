using Silmoon.Extensions.Hosting.Interfaces;
using Silmoon.Maui.Platforms.Services;
using Silmoon.Maui.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.MauiClient.Services
{
    public class SilmoonPlatformDirectoryServiceImpl : ISilmoonPlatformDirectoryService
    {
        IFileService FileService { get; set; }

        public string AppConfigDirectory => FileSystem.AppDataDirectory;

        public string AppDataDirectory => FileSystem.AppDataDirectory;

        public string AppWorkingDirectory => FileSystem.AppDataDirectory;

        public SilmoonPlatformDirectoryServiceImpl(IFileService fileService)
        {
            FileService = fileService;
            FileService.CopyResourceRawFilesToAppData([
                ("config.json", true),
                ("config.local.json", true),
                ("config.debug.json", true),
                ("config.local.debug.json", true),
                (Path.Combine("workspaces","markdowns", "工具疑惑与解惑手册.md"), false),
                (Path.Combine("workspaces","system_prompts", "supervisor_agent_system.md"), false),
                (Path.Combine("workspaces","system_prompts", "unified_agent_system.md"), false),
            ]).Wait();
        }
    }
}

