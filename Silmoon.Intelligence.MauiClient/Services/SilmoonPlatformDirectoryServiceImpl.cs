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
            ]).Wait();
        }
    }
}
