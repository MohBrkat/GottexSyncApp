using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncApp.Helpers
{
    public static class EmailMessages
    {
        public static string messageBody(string operationName, string status, string fileName)
        {
            string body = "Hi - <br /><br />Operation: " + operationName + "<br />";
            body += "Status: " + status + "<br />";
            body += "Log File Location: " + fileName + "<br /><br />";
            body += "Thank you<br />";
            body += "Gottex";

            return body;
        }

        public static string ReportEmailMessageBody()
        {
            string body = "Hi - <br /><br /> Detailed and Summraized Report Files Generated. <br />";
            body += "Please Find them in the attachments <br /><br />";
            body += "Thank you<br />";
            body += "Gottex";

            return body;
        }

        public static string NoOrdersEmailMessageBody()
        {
            string body = "Hi - <br /><br /> ";
            body += "No Such Orders To FulFill <br /><br />";
            body += "Thank you<br />";
            body += "Gottex";

            return body;
        }
    }
}