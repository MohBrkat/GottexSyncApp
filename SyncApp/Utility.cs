using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Log4NetLibrary;
using Renci.SshNet;

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
                // Do something such as log error, but this is based on OP's original code
                // so for now we do nothing.
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
                //Console.WriteLine(ex.ToString());
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
                            //var fileAlreadyUploaded = checkFileName(Path.GetFileNameWithoutExtension(remoteFileName),"/Logs/", connectionInfo);
                            //if (fileAlreadyUploaded)
                            //{
                            //    throw new Exception("File was already uploaded");
                            //}

                            var fileName = "inventory-update-" + DateTime.Today.ToString("yyMMdd") + ".dat";
                            realfileName = fileName;
                            //if (File.Exists("" + fileName))
                            //{
                            //    File.Delete("" + fileName);
                            //}
                            string fileContent = "";
                            //using (Stream s = new MemoryStream())
                            //{
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
                    // if overrite , then delete old one, and create new one
                    //if (true && _client.Exists(fileName))
                    //{
                    //    _client.DeleteFile(fileName);
                    //}

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

        public static void SendEmail(string host, int port, string email, string password, string displayName, string to, string message, string subject, byte[] logFile = null)
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

            if (logFile != null)
            {
                Attachment att = new Attachment(new MemoryStream(logFile), "logs.txt");
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
                            if (arr.Length == 3)
                            {
                                var date = new DateTime();
                                var valid = DateTime.TryParseExact(arr[2], "yyMMddHHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
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
                        return result.First()+".dat";
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
                //Console.WriteLine(ex.ToString());
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
            string url = Host + "/" + FolderPath+"/";
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
                // Do something such as log error, but this is based on OP's original code
                // so for now we do nothing.
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

            // Read the file's contents into a byte array.


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

                // Read the file's contents into a byte array.


                // Write the bytes into the request stream.
                request.ContentLength = file.Length;

                using (Stream request_stream = request.GetRequestStream())
                {
                    request_stream.Write(file, 0, file.Length);

                    request_stream.Close();
                }
                return true;
            }
            catch(Exception ex)
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
