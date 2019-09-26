using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using Hangfire;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShopifyApp2.Controllers;
using SyncApp;

namespace ShopifyApp2
{
    public class Program
    {
        private static Random random = new Random();
        public static void Main(string[] args)
        {
            XmlDocument log4netConfig = new XmlDocument();
            log4netConfig.Load(File.OpenRead("log4net.config"));
            var repo = log4net.LogManager.CreateRepository(Assembly.GetEntryAssembly(),
                       typeof(log4net.Repository.Hierarchy.Hierarchy));
            log4net.Config.XmlConfigurator.Configure(repo, log4netConfig["log4net"]);

            CreateWebHostBuilder(args).Build().Run();
        }

        

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>();

        public static int GetRandomNumber(int min, int max)
        {
            return random.Next(min, max);
        }
        public static IWebHost BuildWebHost(string[] args) =>
    WebHost.CreateDefaultBuilder(args)
        .UseStartup<Startup>()
        .UseKestrel(o => { o.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(30); })
        .Build();
    }


    public class RandomHelper
    {
        public int Get(int min, int max)
        {
            Random random = new Random();
            return random.Next(min, max);
        }


    }
}




