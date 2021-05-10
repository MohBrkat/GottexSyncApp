using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Log4NetLibrary;
using NPOI.HSSF.UserModel;
using NPOI.HSSF.Util;
using NPOI.SS.UserModel;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using Renci.SshNet;
using ShopifySharp;

namespace SyncApp
{
    public class Utility
    {
        private static readonly log4net.ILog _log = Logger.GetLogger();

        public static string ReadLatestFileFromFtp(string Host, string UserName, string Password, string FolderPath)
        {
            WebClient request = new WebClient();
            string url = Host + "/" + FolderPath;
            request.Credentials = new NetworkCredential(UserName, Password);
            try
            {
                url = url + GetLastFileName(Host + "/" + FolderPath, UserName, Password);
                byte[] newFileData = request.DownloadData(url);
                string fileString = System.Text.Encoding.UTF8.GetString(newFileData);
                return fileString;
            }
            catch (WebException e)
            {
                throw e;
            }
        }

        private static string GetLastFileName(string Host, string UserName, string Password)
        {
            FtpWebRequest ftpRequest = (FtpWebRequest)FtpWebRequest.Create(Host);

            /* Log in to the FTP Server with the User Name and Password Provided */
            ftpRequest.Credentials = new NetworkCredential(UserName, Password);

            /* When in doubt, use these options */
            ftpRequest.UseBinary = true;
            ftpRequest.UsePassive = true;
            ftpRequest.KeepAlive = true;

            /* Specify the Type of FTP Request */
            ftpRequest.Method = WebRequestMethods.Ftp.ListDirectoryDetails;

            /* Establish Return Communication with the FTP Server */
            FtpWebResponse ftpResponse = (FtpWebResponse)ftpRequest.GetResponse();

            /* Establish Return Communication with the FTP Server */
            Stream ftpStream = ftpResponse.GetResponseStream();

            /* Get the FTP Server's Response Stream */
            StreamReader ftpReader = new StreamReader(ftpStream);

            /* Store the Raw Response */
            string directoryRaw = null;

            /* Read Each Line of the Response and Append a Pipe to Each Line for Easy Parsing */
            try
            {
                while (ftpReader.Peek() != -1)
                {
                    directoryRaw += ftpReader.ReadLine() + "|";
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex.Message);
            }

            /* Resource Cleanup */
            ftpReader.Close();
            ftpStream.Close();
            ftpResponse.Close();
            ftpRequest = null;

            /* Return the Directory Listing as a string Array by Parsing 'directoryRaw' with the Delimiter you Append (I use | in This Example) */
            try
            {
                string[] directoryList = directoryRaw.Split("|".ToCharArray());
                Array.Sort(directoryList);

                return directoryList.First();
            }
            catch (Exception ex)
            {
                return "";
            }
        }

        internal static string GetFileContentIfExists(string host, string user, string password, string FolderPath, out string realfileName)
        {
            string remoteDirectory = "/" + FolderPath + "/";
            var connectionInfo = new ConnectionInfo(host, "sftp", new PasswordAuthenticationMethod(user, password));
            using (SftpClient client = new SftpClient(connectionInfo))
            {
                client.Connect();
                if (client.IsConnected)
                {
                    var files = client.ListDirectory(remoteDirectory).OrderByDescending(l => l.LastWriteTime);

                    foreach (var file in files)
                    {
                        string remoteFileName = file.Name;
                        string dateNow = file.LastWriteTime.ToString("yyMMdd");
                        var expectedFileName = "inventory-update-" + DateTime.Now.ToString("yyMMdd") + ".dat";
                        if (remoteFileName == expectedFileName && dateNow == DateTime.Now.ToString("yyMMdd"))
                        {
                            var fileName = "inventory-update-" + DateTime.Today.ToString("yyMMdd") + ".dat";
                            realfileName = fileName;
                            string fileContent = "";
                            if (client.Exists(remoteDirectory + remoteFileName))
                            {
                                fileContent = ReadFile(remoteDirectory + remoteFileName, client);
                            }
                            else
                            {
                                return string.Empty;
                            }
                            return fileContent;
                        }
                    }
                }
                realfileName = "unknown";
                return string.Empty;
            }
        }

        public static string ReadFile(string filePath, SftpClient client)
        {
            string text = string.Empty;
            // create stream in memory
            using (MemoryStream stream = new MemoryStream())
            {
                client.DownloadFile(filePath, stream);
                byte[] data = stream.GetBuffer();
                text = System.Text.UTF8Encoding.UTF8.GetString(data);
                stream.Close();
                return text;
            }
        }

        private static bool checkFileName(string remoteFileName, string remoteDirectory, ConnectionInfo connectionInfo)
        {
            using (SftpClient client = new SftpClient(connectionInfo))
            {
                client.Connect();
                if (client.IsConnected)
                {
                    var files = client.ListDirectory(remoteDirectory);
                    foreach (var file in files)
                    {
                        if (file.Name.Contains(remoteFileName))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        public static void UploadLogFile(string host, string username, string password, int port, string fileContent, string fileName, string directoryPath = "Logs")
        {
            using (SftpClient _client = new SftpClient(host, port, username, password))
            {

                _client.Connect();
                _client.ChangeDirectory(directoryPath);

                byte[] byteArray = Encoding.UTF8.GetBytes(fileContent);

                using (MemoryStream stream = new MemoryStream(byteArray))
                {
                    _client.BufferSize = 4 * 1024;
                    _client.UploadFile(stream, fileName);
                    stream.Close();
                }
            }
        }

        public static void ArchiveFile(string host, string username, string password, int port, string fileName, string directoryPath = "")
        {
            using (SftpClient _client = new SftpClient(host, port, username, password))
            {
                _client.Connect();
                _client.RenameFile(fileName, fileName.Replace(".dat", ".dat.archive"));
            }
        }

        public static void UploadSFTPFile(string host, string username,
            string password, string sourcefile, string destinationpath, int port)
        {
            try
            {
                using (SftpClient client = new SftpClient(host, port, username, password))
                {
                    client.Connect();
                    if (client.IsConnected)
                    {
                        client.ChangeDirectory(destinationpath);
                        using (FileStream fs = new FileStream(sourcefile, FileMode.Open))
                        {
                            client.BufferSize = 63 * 1024;
                            client.UploadFile(fs, Path.GetFileName(sourcefile));

                        }
                    }
                }

            }
            catch (Exception e)
            {
                _log.Error(e.Message);
            }
        }

        public static void SendEmail(string host, int port, string email, string password, string displayName, string to, string message, string subject, byte[] successLogFile = null, byte[] failedLogFile = null)
        {
            SmtpClient smtpClient = new SmtpClient(host, port);
            smtpClient.UseDefaultCredentials = false;
            smtpClient.Credentials = new System.Net.NetworkCredential(email, password);
            smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
            smtpClient.EnableSsl = true;

            MailMessage mail = new MailMessage();

            //Setting From , To and CC
            mail.From = new MailAddress(email, displayName);
            mail.To.Add(new MailAddress(to));
            mail.Subject = subject;
            mail.Body = message;
            mail.IsBodyHtml = true;

            if (successLogFile != null)
            {
                Attachment att = new Attachment(new MemoryStream(successLogFile), "SuccessLogs.txt");
                mail.Attachments.Add(att);
            }

            if (failedLogFile != null)
            {
                Attachment att = new Attachment(new MemoryStream(failedLogFile), "FailedLogs.txt");
                mail.Attachments.Add(att);
            }

            smtpClient.Send(mail);
        }

        public static void SendReportEmail(string host, int port, string email, string password, string displayName, string to1, string to2, string message, string subject, string detaileFileName, byte[] detailedFIle, string summarizedFileName, byte[] summarizedFile)
        {
            SmtpClient smtpClient = new SmtpClient(host, port);
            smtpClient.UseDefaultCredentials = false;
            smtpClient.Credentials = new System.Net.NetworkCredential(email, password);
            smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
            smtpClient.EnableSsl = true;

            MailMessage mail = new MailMessage();

            //Setting From , To and CC
            mail.From = new MailAddress(email, displayName);
            mail.To.Add(new MailAddress(to1));
            mail.To.Add(new MailAddress(to2));
            mail.Subject = subject;
            mail.Body = message;
            mail.IsBodyHtml = true;

            if (detailedFIle != null)
            {
                Attachment att = new Attachment(new MemoryStream(detailedFIle), detaileFileName);
                mail.Attachments.Add(att);
            }

            if (summarizedFile != null)
            {
                Attachment att = new Attachment(new MemoryStream(summarizedFile), summarizedFileName);
                mail.Attachments.Add(att);
            }
            smtpClient.Send(mail);
        }

        internal static bool MakeRequest(string host, string user, string password)
        {
            var connectionInfo = new ConnectionInfo(host, 22, user, new PasswordAuthenticationMethod(user, password));
            try
            {
                using (SftpClient client = new SftpClient(connectionInfo))
                {
                    client.Connect();
                    if (client.IsConnected)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }

                }
            }
            catch (Exception e)
            {
                return false;
            }

        }

        public static void removeManualLogs(string fileName, string path)
        {
            DirectoryInfo di = new DirectoryInfo(path);
            foreach (FileInfo file in di.GetFiles())
            {
                if (file.FullName.Contains(fileName))
                {
                    file.Delete();
                }
            }
        }

        public static byte[] ExportToExcel<T>(List<T> list, string extension)
        {
            if (extension == "xlsx")
            {
                return ExportXlsxReport(list);
            }
            else if (extension == "xls")
            {
                return ExportXlsReport(list);
            }
            else if (extension == "csv")
            {
                return ExportCsvReport(list);
            }
            else
            {
                return null;
            }

        }

        public static List<List<T>> Split<T>(List<T> collection, int size)
        {
            Logger.EnterScope();

            var chunks = new List<List<T>>();
            var chunkCount = collection.Count() / size;

            if (collection.Count % size > 0)
                chunkCount++;

            for (var i = 0; i < chunkCount; i++)
                chunks.Add(collection.Skip(i * size).Take(size).ToList());

            Logger.ExitScope();

            return chunks;
        }

        public static byte[] ExportXlsxReport<T>(List<T> list)
        {
            var stream = new MemoryStream();

            using (ExcelPackage package = new ExcelPackage(stream))
            {

                ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("report");
                worksheet.PrinterSettings.FitToPage = true;
                worksheet.PrinterSettings.PaperSize = ePaperSize.A4;
                worksheet.PrinterSettings.ShowGridLines = true;
                worksheet.Protection.IsProtected = false;

                int totalRows = list.Count;

                var properties = typeof(T).GetProperties().ToList();

                worksheet.Column(1).Style.Border.Left.Style = ExcelBorderStyle.Thin;
                worksheet.Row(1).Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                worksheet.Row(1).Style.Border.Top.Style = ExcelBorderStyle.Thin;

                worksheet.Column(properties.Count).Style.WrapText = true;

                for (int j = 1; j <= properties.Count; j++)
                {
                    worksheet.Cells[1, j].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    worksheet.Cells[1, j].Style.Font.Bold = true;
                    worksheet.Cells[1, j].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[1, j].Style.Fill.BackgroundColor.SetColor(Color.LightYellow);
                    worksheet.Column(j).Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    worksheet.Column(j).Width = 30;
                    worksheet.Column(j).Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    worksheet.Cells[1, j].Value = Regex.Replace(Regex.Replace(properties[j - 1].Name, @"(\P{Ll})(\P{Ll}\p{Ll})", "$1 $2"), @"(\p{Ll})(\P{Ll})", "$1 $2"); ;
                }

                int i = 0;

                for (int row = 2; row <= totalRows + 1; row++)
                {
                    worksheet.Row(row).Style.Border.Bottom.Style = ExcelBorderStyle.Thin;

                    for (int j = 1; j <= properties.Count; j++)
                    {
                        worksheet.Cells[row, j].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                        worksheet.Cells[row, j].Value = list[i].GetType()
                            .GetProperty(properties[j - 1].Name)
                            ?.GetValue(list[i]);

                        var type = properties[j - 1].Name;
                        if (type.Trim().ToLower() == "productbarcode")
                        {
                            worksheet.Cells[row, j].Formula = "\"" + worksheet.Cells[row, j].Value.ToString() + "\"";
                        }

                        if (worksheet.Cells[row, j].Value is DateTime)
                            worksheet.Cells[row, j].Value = ((DateTime)worksheet.Cells[row, j].Value).ToShortDateString();
                    }

                    i++;
                }

                package.Workbook.Calculate();
                return package.GetAsByteArray();
            }
        }

        public static byte[] ExportCsvReport<T>(List<T> list)
        {
            var stream = new MemoryStream();
            using (ExcelPackage package = new ExcelPackage(stream))
            {

                ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("report");
                int totalRows = list.Count;

                var properties = typeof(T).GetProperties().ToList();
                StringBuilder sb = new StringBuilder();

                int i = 0;

                for (int row = 1; row <= totalRows; row++)
                {
                    for (int j = 1; j <= properties.Count; j++)
                    {
                        worksheet.Cells[row, j].Value = list[i].GetType()
                            .GetProperty(properties[j - 1].Name)
                            ?.GetValue(list[i]);

                        if (IsExponentialFormat(worksheet.Cells[row, j].Value.ToString()) || double.TryParse(worksheet.Cells[row, j].Value.ToString(), out double dummy))
                        {
                            worksheet.Cells[row, j].Style.Numberformat.Format = "0";
                            double convertedValue = Convert.ToDouble(worksheet.Cells[row, j].Value);
                            worksheet.Cells[row, j].Value = convertedValue;
                        }

                        if (worksheet.Cells[row, j].Value is DateTime)
                            worksheet.Cells[row, j].Value = ((DateTime)worksheet.Cells[row, j].Value).ToShortDateString();

                        if (j == properties.Count)
                        {
                            sb.Append(worksheet.Cells[row, j].Value);
                        }
                        else
                        {
                            sb.Append(worksheet.Cells[row, j].Value + ",");
                        }
                    }
                    sb.AppendLine();
                    i++;
                }

                return Encoding.ASCII.GetBytes(sb.ToString());
            }
        }

        public static byte[] ExportXlsReport<T>(List<T> list)
        {
            IWorkbook workbook;
            workbook = new HSSFWorkbook();
            ISheet sheet1 = workbook.CreateSheet("Sheet 1");
            IRow row1 = sheet1.CreateRow(0);

            var font = workbook.CreateFont();
            font.FontName = "Calibri";
            font.FontHeightInPoints = 11;
            font.Boldweight = (short)FontBoldWeight.Bold;

            row1.RowStyle.BorderTop = BorderStyle.Thin;

            var properties = typeof(T).GetProperties().ToList();
            for (int j = 0; j < properties.Count; j++)
            {
                ICell cell = row1.CreateCell(j);
                cell.CellStyle.Alignment = HorizontalAlignment.Center;
                cell.CellStyle.FillBackgroundColor = HSSFColor.LightYellow.Index;
                cell.CellStyle.SetFont(font);
                cell.CellStyle.FillPattern = FillPattern.SolidForeground;
                cell.CellStyle.BorderRight = BorderStyle.Thin;
                sheet1.SetColumnWidth(j, 30);

                cell.SetCellValue(Regex.Replace(Regex.Replace(properties[j].Name, @"(\P{Ll})(\P{Ll}\p{Ll})", "$1 $2"), @"(\p{Ll})(\P{Ll})", "$1 $2"));
            }

            for (int r = 0; r < list.Count; r++)
            {
                IRow row = sheet1.CreateRow(r + 1);
                row.RowStyle.BorderBottom = BorderStyle.Thin;

                for (int j = 0; j < properties.Count; j++)
                {
                    ICell cell = row.CreateCell(j);
                    var value = list[r].GetType().GetProperty(properties[j].Name)?.GetValue(list[r]);

                    if (IsExponentialFormat(value.ToString()) || double.TryParse(value.ToString(), out double dummy))
                    {
                        cell.CellStyle.DataFormat = HSSFDataFormat.GetBuiltinFormat("0");
                        double convertedValue = Convert.ToDouble(value);
                        value = convertedValue;
                    }

                    cell.CellStyle.BorderRight = BorderStyle.Thin;
                    cell.CellStyle.Alignment = HorizontalAlignment.Center;

                    if (value is DateTime)
                        value = ((DateTime)value).ToShortDateString();

                    cell.SetCellValue(value.ToString());
                }
            }

            using (var exportData = new MemoryStream())
            {
                workbook.Write(exportData);
                return exportData.GetBuffer();
            }
        }

        public static bool IsExponentialFormat(string str)
        {
            double dummy;
            return (str.Contains("E") || str.Contains("e")) && double.TryParse(str, out dummy);
        }

        internal static void SendEmail(string smtpHost, int smtpPort, string emailUserName, string emailPassword, string displayName, string reportEmailAddress1, string reportEmailAddress2, string body, string subject)
        {
            try
            {
                SmtpClient smtpClient = new SmtpClient(smtpHost, smtpPort);
                smtpClient.UseDefaultCredentials = false;
                smtpClient.Credentials = new System.Net.NetworkCredential(emailUserName, emailPassword);
                smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                smtpClient.EnableSsl = true;

                MailMessage mail = new MailMessage();

                //Setting From , To and CC
                mail.From = new MailAddress(emailUserName, displayName);
                mail.To.Add(new MailAddress(reportEmailAddress1));
                mail.To.Add(new MailAddress(reportEmailAddress2));
                mail.Subject = subject;
                mail.Body = body;
                mail.IsBodyHtml = true;

                smtpClient.Send(mail);

            }
            catch (Exception e)
            {
                _log.Error(e.Message);
            }
        }
    }

    public class FtpHandler
    {
        private static string GetLastFileName(string Host, string UserName, string Password)
        {
            FtpWebRequest ftpRequest = (FtpWebRequest)FtpWebRequest.Create(Host);

            ftpRequest.Credentials = new NetworkCredential(UserName, Password);

            ftpRequest.UseBinary = true;
            ftpRequest.UsePassive = true;
            ftpRequest.KeepAlive = true;

            ftpRequest.Method = WebRequestMethods.Ftp.ListDirectory;

            FtpWebResponse ftpResponse = (FtpWebResponse)ftpRequest.GetResponse();

            Stream ftpStream = ftpResponse.GetResponseStream();

            StreamReader ftpReader = new StreamReader(ftpStream);

            List<string> ls = new List<string>();

            try
            {
                while (ftpReader.Peek() != -1)
                {
                    ls.Add(ftpReader.ReadLine());
                }

                var lsOfValid = new List<string>();

                if (ls.Count > 0)
                {
                    foreach (var item in ls)
                    {
                        string current = item;
                        if (current.EndsWith(".dat"))
                        {
                            current = current.Replace(".dat", "");
                            var arr = current.Split("-");
                            if (arr.Length == 4)
                            {
                                var date = new DateTime();
                                var valid = DateTime.TryParseExact(arr[3], "yyMMddHHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
                                if (valid)
                                {
                                    lsOfValid.Add(current);
                                }
                            }
                        }
                    }

                    if (lsOfValid.Count > 0)
                    {
                        var result = lsOfValid.ToArray();
                        Array.Sort(result);
                        return result.First() + ".dat";
                    }
                    else
                    {
                        return "";
                    }
                }
            }
            catch (Exception ex)
            {
                return "";
            }
            finally
            {
                ftpReader.Close();
                ftpStream.Close();
                ftpResponse.Close();
                ftpRequest = null;
            }
            return "";
        }

        public static string ReadLatestFileFromFtp(string Host, string UserName, string Password, string FolderPath, out string fileName)
        {
            WebClient request = new WebClient();
            string url = Host + "/" + FolderPath + "/";
            request.Credentials = new NetworkCredential(UserName, Password);
            try
            {
                var _fileName = GetLastFileName(Host + "/" + FolderPath, UserName, Password);
                url = url + _fileName;

                byte[] newFileData = request.DownloadData(url);
                string fileString = System.Text.Encoding.UTF8.GetString(newFileData);
                fileName = _fileName;
                return fileString;
            }
            catch (WebException e)
            {
                throw e;
            }
        }

        public static string DwonloadFile(string fileName, string ServerUrl, string path, string userName, string password)
        {
            WebClient request = new WebClient();
            string url = ServerUrl + "/" + path + "/" + fileName;

            request.Credentials = new NetworkCredential(userName, password);

            try
            {
                byte[] newFileData = request.DownloadData(url);
                string fileString = System.Text.Encoding.UTF8.GetString(newFileData);
                return fileString;
            }
            catch (WebException e)
            {
                throw;
            }
        }

        public static void ArchiveFileOld(string fileName, string ServerUrl, string path, string userName, string password)
        {
            FtpWebRequest request =
                   (FtpWebRequest)WebRequest.Create(ServerUrl + "/" + path + "/" + fileName);

            request.Method = WebRequestMethods.Ftp.Rename;

            // Get network credentials.
            request.Credentials = new NetworkCredential(userName, password);

            // Write the bytes into the request stream.
            request.RenameTo = fileName.Replace(".dat", ".dat.archive");

            using (Stream request_stream = request.GetRequestStream())
            {
                request_stream.Close();
            }
        }

        public static void ArchiveFile(string fileName, string ServerUrl, string path, string userName, string password)
        {
            FtpWebRequest reqFTP = null;
            Stream ftpStream = null;
            try
            {
                reqFTP = (FtpWebRequest)FtpWebRequest.Create(ServerUrl + "/" + path + "/" + fileName);

                reqFTP.Method = WebRequestMethods.Ftp.Rename;

                reqFTP.RenameTo = fileName.Replace(".dat", DateTime.Now.Hour + "_" + DateTime.Now.Minute + "_" + DateTime.Now.Second + ".dat.archive");

                reqFTP.UseBinary = true;

                reqFTP.Credentials = new NetworkCredential(userName, password);

                FtpWebResponse response = (FtpWebResponse)reqFTP.GetResponse();

                ftpStream = response.GetResponseStream();

                ftpStream.Close();

                response.Close();

            }

            catch (Exception ex)
            {
                if (ftpStream != null)
                {
                    ftpStream.Close();
                    ftpStream.Dispose();
                }
                throw new Exception(ex.Message.ToString());
            }
        }

        public static bool UploadFile(string fileName, byte[] file, string ServerUrl, string path, string userName, string password)
        {
            try
            {
                // Get the object used to communicate with the server.
                FtpWebRequest request =
                    (FtpWebRequest)WebRequest.Create(ServerUrl + "/" + path + "/" + fileName);

                request.Method = WebRequestMethods.Ftp.UploadFile;

                // Get network credentials.
                request.Credentials = new NetworkCredential(userName, password);

                // Write the bytes into the request stream.
                request.ContentLength = file.Length;

                using (Stream request_stream = request.GetRequestStream())
                {
                    request_stream.Write(file, 0, file.Length);
                    request_stream.Close();
                }
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public static void DeleteFile(string fileName, string ServerUrl, string path, string userName, string password)
        {
            FtpWebRequest reqFTP = null;
            Stream ftpStream = null;
            try
            {
                reqFTP = (FtpWebRequest)FtpWebRequest.Create(ServerUrl + "/" + path + "/" + fileName);

                reqFTP.Method = WebRequestMethods.Ftp.DeleteFile;

                reqFTP.Credentials = new NetworkCredential(userName, password);

                FtpWebResponse response = (FtpWebResponse)reqFTP.GetResponse();

                ftpStream = response.GetResponseStream();

                ftpStream.Close();

                response.Close();
            }
            catch (Exception ex)
            {
                if (ftpStream != null)
                {
                    ftpStream.Close();
                    ftpStream.Dispose();
                }
                throw new Exception(ex.Message.ToString());
            }
        }
    }
}
