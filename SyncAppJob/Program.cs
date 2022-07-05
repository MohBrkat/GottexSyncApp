using SyncAppCommon.Helpers;
using System;

namespace ShopifyJob
{
    class Program
    {
        static string GetFileName()
        {
            return ConfigurationManager.GetConfig("", "TriggerReportKey");
        }

        static void Main(string[] args)
        {
            string fileName = GetFileName();
            Console.WriteLine("Hello World!");
        }
    }
}
