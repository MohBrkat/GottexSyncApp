using Microsoft.Extensions.Configuration;
using SyncAppCommon;
using SyncAppCommon.Helpers;
using SyncAppEntities.Models;
using SyncAppEntities.Models.EF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SyncAppJob
{
    public class SyncAppJobLogic
    {
        private static readonly object syncAppJobLock = new object();

        private Configrations Config
        {
            get
            {
                return _dal.GetConfigurations().First();
            }
        }

        private string Host
        {
            get
            {
                return Config.FtpHost ?? string.Empty;
            }
        }
        private string UserName
        {
            get
            {
                return Config.FtpUserName ?? string.Empty;
            }
        }
        private string Password
        {
            get
            {
                return Config.FtpPassword ?? string.Empty;
            }
        }

        private IConfiguration _configuration;
        private ShopifyAppContext _context;
        private JobDAL _dal;
        public SyncAppJobLogic(IConfiguration configuration, ShopifyAppContext context)
        {
            _configuration = configuration;
            _context = context;
            _dal = new JobDAL(configuration, context);
        }

        internal void GenerateReport(string reportTriggerKey)
        {
            lock (syncAppJobLock)
            {
                if (FtpHandler.CheckIfFileExistsOnServer(Host, UserName, Password, FTPPathConsts.OUT_PATH, reportTriggerKey))
                {
                    string baseAPIUrl = _configuration.GetConfig("AppSettings", "APIBaseUrl");
                    var result = new RestSharpClient(baseAPIUrl).Get<object>("api/SyncApp/GenerateDailyReports", null);

                    if (result != null)
                    {
                        FtpHandler.DeleteFile(reportTriggerKey, Host, FTPPathConsts.OUT_PATH, UserName, Password);
                    }
                }
            }
        }
    }
}
