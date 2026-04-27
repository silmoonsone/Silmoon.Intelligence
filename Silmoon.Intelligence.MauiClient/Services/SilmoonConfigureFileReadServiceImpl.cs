using Silmoon.Extensions.Hosting.Interfaces;
using Silmoon.Maui.Platforms.Services;
using Silmoon.Maui.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.MauiClient.Services
{
    public class SilmoonConfigureFileReadServiceImpl : ISilmoonConfigureFileReadService
    {
        IFileService FileService { get; set; }
        public SilmoonConfigureFileReadServiceImpl(IFileService fileService)
        {
            FileService = fileService;
            FileService.CopyResourceRawFilesToAppData([
                ("config.json", true),
                ("config.local.json", true),
                ("config.debug.json", true),
                ("config.local.debug.json", true),
            ]).Wait();
        }
        public string GetFileContent(string filePath)
        {
            var content = File.ReadAllText(Path.Combine(FileSystem.AppDataDirectory, filePath));
            return content;
        }
    }
}
