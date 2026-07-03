using LiteDB;
using Silmoon.Data.LiteDB;
using Silmoon.Extensions.Hosting.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Silmoon.Intelligence.Hosting.Services
{
    public class Core : LiteDBService
    {
        SilmoonConfigureServiceImpl SilmoonConfigureService { get; set; }
        public Core(ISilmoonConfigureService silmoonConfigureService)
        {
            SilmoonConfigureService = silmoonConfigureService as SilmoonConfigureServiceImpl;
            Database = new LiteDatabase(SilmoonConfigureService.ConnectionString);
        }
    }
}

