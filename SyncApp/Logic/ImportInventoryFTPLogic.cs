using Log4NetLibrary;
using Microsoft.AspNetCore.Hosting;
using ShopifyApp2;
using ShopifySharp;
using ShopifySharp.Filters;
using SyncApp.Helpers;
using SyncApp.Models;
using SyncApp.Models.EF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SyncApp.Logic
{
    public class ImportInventoryFTPLogic
    {
        private readonly ShopifyAppContext _context;
        private static readonly log4net.ILog _log = Logger.GetLogger();
        private static readonly object importInventoryLock = new object();
        public const int MAX_RETRY_COUNT = 5;

        public ImportInventoryFTPLogic(ShopifyAppContext context)
        {
            _context = context;
        }

        private Configrations Config
        {
            get
            {
                return _context.Configrations.First();
            }
        }

        #region prop
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
        private string SmtpHost
        {
            get
            {
                return Config.SmtpHost ?? string.Empty;
            }
        }
        private int SmtpPort
        {
            get
            {
                return Config.SmtpPort.GetValueOrDefault();
            }
        }
        private string EmailUserName
        {
            get
            {
                return Config.SenderEmail ?? string.Empty;
            }
        }
        private string EmailPassword
        {
            get
            {
                return Config.SenderemailPassword ?? string.Empty;
            }
        }
        private string DisplayName
        {
            get
            {
                return Config.DisplayName ?? string.Empty;
            }
        }
        private string ToEmail
        {
            get
            {
                return Config.NotificationEmail ?? string.Empty;
            }
        }
        private string StoreUrl
        {
            get
            {
                return Config.StoreUrl ?? string.Empty;
            }
        }
        private string ApiSecret
        {
            get
            {
                return Config.ApiSecret ?? string.Empty;
            }
        }
        #endregion

        public async Task ImportInventoryFileAsync()
        {
            bool importSuccess = false;

            _log.Info($"Start ImportInventoryFileAsync");
            FileInformation info = await ValidateInventoryUpdatesFromCSVAsync();

            if (info != null && !string.IsNullOrEmpty(info.fileName))
            {
                lock (importInventoryLock)
                {
                    var fileImportStatus = _context.FilesImportStatus.FirstOrDefault(s => s.FileName.Trim() == info.fileName.Trim());

                    if (fileImportStatus != null)
                    {
                        _log.Info($"ImportInventoryFileAsync - fileImportStatus.IsImportSuccess :" + fileImportStatus.IsImportSuccess);
                        if (fileImportStatus.IsImportSuccess == true || fileImportStatus.IsCompleted == false)
                        {                         
                            return;
                        }
                    }
                    else
                    {
                        InsertFileImportStatus(false, false, info);
                    }
                }

                _log.Info("[Inventory] : file name : " + info.fileName + "--" + "discovered and will be processed.");

                if (info.isValid && info.lsErrorCount == 0)
                {
                    importSuccess = await ImportValidInvenotryUpdatesFromCSVAsync(info);
                }

                string subject = info.fileName + " Import Status";

                if (importSuccess)
                {
                    _log.Info($"ImportInventoryFileAsync - importSuccess - Delete File");
                    FtpHandler.DeleteFile(info.fileName, Host, FTPPathConsts.OUT_PATH, UserName, Password);
                    var body = EmailMessages.messageBody("Import inventory File", "success", info.fileName + ".log");
                    _log.Info($"ImportInventoryFileAsync - importSuccess - Send Email");
                    Utility.SendEmail(SmtpHost, SmtpPort, EmailUserName, EmailPassword, DisplayName, ToEmail, body, subject);
                }
                else
                {
                    var successLogFile = Encoding.ASCII.GetBytes(String.Join(Environment.NewLine, info.LsOfSucess.ToArray()));
                    var failedLogFile = Encoding.ASCII.GetBytes(String.Join(Environment.NewLine, info.LsOfErrors.ToArray()));
                    _log.Info($"ImportInventoryFileAsync - Delete File");
                    FtpHandler.DeleteFile(info.fileName, Host, FTPPathConsts.OUT_PATH, UserName, Password);
                    var body = EmailMessages.messageBody("Import inventory File", "failed", info.fileName + ".log");
                    _log.Info($"ImportInventoryFileAsync - Send Email");
                    Utility.SendEmail(SmtpHost, SmtpPort, EmailUserName, EmailPassword, DisplayName, ToEmail, body, subject, successLogFile, failedLogFile);
                }

                UpdateFileImportStatus(importSuccess, true, info);
            }
            _log.Info($"End ImportInventoryFileAsync");
        }
        private void InsertFileImportStatus(bool importSuccess, bool isCompleted, FileInformation info)
        {
            try
            {
                _log.Info($"Start InsertFileImportStatus");
                FilesImportStatus importStatus = new FilesImportStatus()
                {
                    FileName = info.fileName,
                    IsCompleted = isCompleted,
                    IsImportSuccess = importSuccess,
                    CreateDate = DateTime.Now,
                    UpdateDate = DateTime.Now
                };

                _context.Add(importStatus);
                _context.SaveChanges();
                _log.Info($"End InsertFileImportStatus");
            }
            catch (Exception e)
            {
                _log.Error(e.Message);
            }
        }
        private void UpdateFileImportStatus(bool importSuccess, bool isCompleted, FileInformation info)
        {
            try
            {
                _log.Info($"Start UpdateFileImportStatus");
                FilesImportStatus importStatus = _context.FilesImportStatus.FirstOrDefault(f => f.FileName.Trim() == info.fileName.Trim());

                if (importStatus != null)
                {
                    importStatus.IsCompleted = isCompleted;
                    importStatus.IsImportSuccess = importSuccess;
                    importStatus.UpdateDate = DateTime.Now;

                    _context.SaveChanges();
                    _log.Info($"End UpdateFileImportStatus");
                }
            }
            catch (Exception e)
            {
                _log.Error(e.Message);
            }
        }
        public async Task<FileInformation> ValidateInventoryUpdatesFromCSVAsync()
        {
           
            _log.Info($"Start ValidateInventoryUpdatesFromCSVAsync");
            List<string> LsOfSuccess = new List<string>();
            List<string> LsOfErrors = new List<string>();

            List<string> fileRows = new List<string>();

            FileInformation info = new FileInformation();
            bool validFile = false;

            info.fileName = "";

            try
            {
                var fileName = "";
                var fileContent = FtpHandler.ReadLatestFileFromFtp(Host, UserName, Password, FTPPathConsts.OUT_PATH, out fileName);

                info.fileName = fileName;

                if (!string.IsNullOrEmpty(fileContent))
                {
                    _log.Info($"ValidateInventoryUpdatesFromCSVAsync - Send Email");
                    Utility.SendEmail(SmtpHost, SmtpPort, EmailUserName, EmailPassword, DisplayName, ToEmail, $"Inventory update starting with the file {info.fileName}", "processing " + info.fileName + " has been satrted.");

                    var Rows = fileContent.Split(Environment.NewLine).ToArray(); // skip the header

                    var ProductServices = new ProductService(StoreUrl, ApiSecret);
                    var InventoryLevelsServices = new InventoryLevelService(StoreUrl, ApiSecret);

                    var Headers = Rows[0];
                    if (ValidateCSV.IsValidHeaders(Headers))
                    {
                        Rows = Rows.Last().Equals("") ? Rows.SkipLast(1).ToArray() : Rows;
                        Rows = Rows.Last().Contains("\0") ? Rows.SkipLast(1).ToArray() : Rows;

                        Rows = Rows.Skip(1).ToArray();// skip headers

                        fileRows = Rows.ToList();

                        int rowIndex = 1;

                        var Products = await GetProductsAsync();

                        foreach (var row in Rows)
                        {
                            try
                            {
                                if (row.IsNotNullOrEmpty() && ValidateCSV.IsValidRow(row))
                                {
                                    var splittedRow = row.Split(',');
                                    string Handle = splittedRow[0];
                                    string Sku = splittedRow[1];
                                    string Method = splittedRow[2];
                                    string Quantity = splittedRow[3];

                                    var ProductObj = Products.FirstOrDefault(p => p.Handle.ToLower().StartsWith(Handle.ToLower()));

                                    if (ProductObj == null)
                                    {
                                        throw new Exception(string.Format("Product {0} not exists.", Handle, rowIndex));
                                    }
                                    var VariantObj = ProductObj.Variants.FirstOrDefault(a => a.SKU == Sku);
                                    if (VariantObj == null)
                                    {
                                        throw new Exception(string.Format("Variant {0} not exists.", Sku, rowIndex));
                                    }
                                    if (Method.ToLower().Trim() == "set")
                                    {
                                        LsOfSuccess.Add(string.Format("Row# {0}-Inventory {1}.", rowIndex, "will be updated"));
                                    }
                                    else
                                    if (Method.ToLower().Trim() == "in")
                                    {
                                        LsOfSuccess.Add(string.Format("Row# {0}-Inventory {1}.", rowIndex, "will be updated"));
                                    }
                                    else
                                    if (Method.ToLower().Trim() == "out")
                                    {
                                        LsOfSuccess.Add(string.Format("Row# {0}-Inventory {1}.", rowIndex, "will be updated"));
                                    }
                                    else
                                    {
                                        throw new Exception(string.Format("Method {0} not defined.", Method, rowIndex));
                                    }
                                }
                                else
                                {
                                    throw new Exception(string.Format("Empty or invalid row.", rowIndex));
                                }
                            }
                            catch (Exception ex)
                            {
                                LsOfErrors.Add("error occured in the row# " + rowIndex + " : " + ex.Message);
                            }
                            rowIndex++;
                        }
                    }
                    else
                    {
                        LsOfErrors.Add("Invalid csv file! ");
                    }

                    if (LsOfErrors != null && LsOfErrors.Count == 0)
                    {
                        LsOfSuccess.Add("Validating Success");
                        validFile = true;
                    }
                    else
                    {
                        LsOfErrors.Add("Validating completed with errors");
                        validFile = false;
                    }
                }
                else
                {
                    throw new Exception(string.Format("File was empty or not found"));
                }
            }
            catch (System.Net.WebException ex)
            {
                _log.Error(ex.Message);
            }
            catch (Exception ex)
            {
                LsOfErrors.Clear();
                LsOfSuccess.Clear();

                LsOfErrors.Add("Error While Validating The File : " + ex.Message);
            }

            if (LsOfErrors.Count > 0)
            {
                LsOfErrors.Insert(0, info.fileName);
            }

            foreach (var ls in LsOfErrors)
            {
                _log.Error(ls);
            }

            info.fileRows = fileRows;
            info.lsErrorCount = LsOfErrors.Count();
            info.isValid = validFile;
            info.LsOfErrors = LsOfErrors;
            _log.Info($"End ValidateInventoryUpdatesFromCSVAsync");
            return info;
        }

        private async Task<bool> ImportValidInvenotryUpdatesFromCSVAsync(FileInformation info, int retryCount = 0)
        {
            _log.Info($"Start ImportValidInvenotryUpdatesFromCSVAsync");
            List<string> RowsWithoutHeader = info.fileRows;

            var InventoryLevelsServices = new InventoryLevelService(StoreUrl, ApiSecret);

            info.LsOfSucess.Add("[Inventory] : file name : " + info.fileName + "--" + "discovered and will be processed, rows count: " + RowsWithoutHeader.Count);
            info.LsOfErrors.Add("[Inventory] : file name : " + info.fileName + "--" + "discovered and will be processed, rows count: " + RowsWithoutHeader.Count);

            int rowIndex = 1;

            var Products = await GetProductsAsync();

            for (int i = 0; i < RowsWithoutHeader.Count;)
            {
                try
                {
                    var row = RowsWithoutHeader[i];

                    var splittedRow = row.Split(',');

                    string Handle = splittedRow[0];
                    string Sku = splittedRow[1];
                    string Method = splittedRow[2];
                    string Quantity = splittedRow[3];

                    var ProductObj = Products.FirstOrDefault(p => p.Handle.ToLower().StartsWith(Handle.ToLower()));
                    var VariantObj = ProductObj.Variants.FirstOrDefault(a => a.SKU == Sku);
                   
                    var InventoryItemIds = new List<long>() { VariantObj.InventoryItemId.GetValueOrDefault() };
                    var InventoryItemId = new List<long>() { VariantObj.InventoryItemId.GetValueOrDefault() }.FirstOrDefault();

                    var LocationQuery = await InventoryLevelsServices.ListAsync(new InventoryLevelListFilter { InventoryItemIds = InventoryItemIds });
                    var LocationId = LocationQuery.Items.FirstOrDefault().LocationId;

                    if (Method.ToLower().Trim() == "set")
                    {
                        var Result = await InventoryLevelsServices.SetAsync(new InventoryLevel { LocationId = LocationId, InventoryItemId = InventoryItemId, Available = Convert.ToInt32(Quantity) });
                    }
                    else if (Method.ToLower().Trim() == "in")
                    {
                        var Result = await InventoryLevelsServices.AdjustAsync(new InventoryLevelAdjust { LocationId = LocationId, InventoryItemId = InventoryItemId, AvailableAdjustment = Convert.ToInt32(Quantity) });
                    }
                    else if (Method.ToLower().Trim() == "out")
                    {
                        var Result = await InventoryLevelsServices.AdjustAsync(new InventoryLevelAdjust { LocationId = LocationId, InventoryItemId = InventoryItemId, AvailableAdjustment = Convert.ToInt32(Quantity) * -1 });
                    }

                    _log.Info("the handle : " + Handle + "--" + "processed, row#: " + rowIndex);

                    info.LsOfSucess.Add("the handle : " + Handle + "--" + "processed, row#: " + rowIndex);

                    i++;
                    rowIndex++;
                }
                catch (Exception ex)
                {
                    retryCount++;

                    if (retryCount >= MAX_RETRY_COUNT)
                    {
                        _log.Error("error occured in the row# " + rowIndex + " : " + ex.Message);
                        info.LsOfErrors.Add("error occured in the row# " + rowIndex + " : " + ex.Message);
                        retryCount = 0;
                        return false;
                    }
                }
            }

            _log.Info("file processed sucesfully");

            info.LsOfSucess.Add("file: " + info.fileName + "processed sucesfully");

            _log.Info($"End ImportValidInvenotryUpdatesFromCSVAsync");
            return true;
        }

        public async Task<List<Product>> GetProductsAsync()
        {
            return await new GetShopifyProducts(StoreUrl, ApiSecret).GetProductsAsync();
        }
    }
}