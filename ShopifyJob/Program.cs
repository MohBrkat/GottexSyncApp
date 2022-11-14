using Log4NetLibrary;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SyncAppCommon;
using SyncAppCommon.Helpers;
using SyncAppEntities.Models;
using SyncAppEntities.Models.EF;
using SyncAppJob;
using System;
using System.IO;
using System.Linq;

namespace ShopifyJob
{
    class Program
    {
        private static IConfiguration _configuration;
        private static JobDAL _dal;
        private static readonly log4net.ILog _log = Logger.GetLogger();

        static void Main(string[] args)
        {
            try
            {
                _configuration = ConfigurationHelper.GetAppSettingsFile();

                string reportTriggerKey = _configuration.GetConfig("AppSettings", "ReportTriggerKey");

                _log.Info($"Report trigger Key found: {reportTriggerKey}");

                if (!string.IsNullOrEmpty(reportTriggerKey))
                {
                    var context = GetShopifyContext();

                    SyncAppJobLogic logic = new SyncAppJobLogic(_configuration, context);

                    logic.GenerateReport(reportTriggerKey);
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Exception while Executing SyncApp Job: {ex.Message}", ex);
            }
        }

        private static ShopifyAppContext GetShopifyContext()
        {
            string connectionString = _configuration.GetConnectionString("DbConnection");

            var services = new ServiceCollection();
            services.AddDbContext<ShopifyAppContext>(options => options.UseSqlServer(connectionString));
            var serviceProvider = services.BuildServiceProvider();

            var context = serviceProvider.GetService<ShopifyAppContext>();
            return context;
        }
    }
}
