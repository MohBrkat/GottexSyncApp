using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SyncAppCommon.Helpers
{
    public static class ConfigurationHelper
    {
        public static IConfigurationRoot GetAppSettingsFile()
        {
            var builder = new ConfigurationBuilder()
                 .SetBasePath(Directory.GetCurrentDirectory())
                 .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            return builder.Build();
        }

        public static string GetConfig(this IConfiguration configuration, string parent, string child)
        {
            return configuration[parent + ":" + child];
        }

    }
}
