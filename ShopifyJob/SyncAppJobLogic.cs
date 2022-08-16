using Log4NetLibrary;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
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
        private static readonly log4net.ILog _log = Logger.GetLogger();

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
            _log.Info($"job is starting");

            lock (syncAppJobLock)
            {
                if (FtpHandler.CheckIfFileExistsOnServer(Host, UserName, Password, FTPPathConsts.OUT_PATH, reportTriggerKey))
                {
                    _log.Info($"File {reportTriggerKey} found, Report in progress");

                    string baseAPIUrl = _configuration.GetConfig("AppSettings", "APIBaseUrl");
                    var result = new RestSharpClient(baseAPIUrl).Get<object>("api/SyncApp/GenerateDailyReports", null);

                    if (result != null)
                    {
                        _log.Info($"Job Completed, Result: {JsonConvert.SerializeObject(result)}, Deleting file from FTP");
                        FtpHandler.DeleteFile(reportTriggerKey, Host, FTPPathConsts.OUT_PATH, UserName, Password);
                    }
                }
            }
        }
    }
}
