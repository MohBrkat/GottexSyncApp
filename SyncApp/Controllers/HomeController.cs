using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShopifyApp2.ViewModel;
using ShopifySharp;
using SyncApp;
using SyncApp.Models;
using Log4NetLibrary;
using SyncApp.Models.EF;
using Microsoft.Extensions.Caching.Memory;
using SyncApp.Controllers;
using SyncApp.Filters;
using System.Text;
using SyncApp.ViewModel;
using Newtonsoft.Json;
using System.Threading.Tasks;
using ShopifySharp.Filters;

namespace ShopifyApp2.Controllers
{
    [Auth]
    public class HomeController : Controller
    {
        private readonly ShopifyAppContext _context;
        private readonly IHostingEnvironment _hostingEnvironment;
        private static readonly object reciptsFileLock = new object();
        private static readonly object salesFileLock = new object();
        private static readonly object importInventoryLock = new object();

        private static readonly log4net.ILog _log = Logger.GetLogger();


        List<string> LsOfManualSuccess = new List<string>();
        List<string> LsOfManualErrors = new List<string>();

        public HomeController(ShopifyAppContext context, IHostingEnvironment hostingEnvironment)
        {
            _context = context;
            _hostingEnvironment = hostingEnvironment;
        }


        #region prop

        private Configrations _config
        {
            get
            {
                return _context.Configrations.First();
            }
        }

        private string WareHouseCode
        {
            get
            {
                return _config.WareHouseCode;
            }
        }
        private string CustomerCode
        {
            get
            {
                return _config.CustoemrCode;
            }
        }
        private string ShortBranchCodeSales
        {
            get
            {
                return _config.BranchcodeSalesInvoices;
            }
        }
        private string ShortBranchCodeRecipt
        {
            get
            {
                return _config.BranchCodeReceipt;
            }
        }
        private string _customerCodeWithLeadingSpaces
        {
            get
            {
                return CustomerCode.InsertLeadingSpaces(16);
            }
        }
        private string _shortBranchCodeSaleswithLeadingzero
        {
            get
            {
                return ShortBranchCodeSales.ToString().InsertLeadingZeros(4);
            }
        }
        private string _shortBranchCodeSalesWithLeadingspaces
        {
            get
            {
                return ShortBranchCodeSales.ToString().InsertLeadingSpaces(8);
            }
        }
        private string _shortBranchCodeReciptswithLeadingzero
        {
            get
            {
                return ShortBranchCodeRecipt.ToString().InsertLeadingZeros(4);
            }
        }
        private string _shortBranchCodeReciptsWithLeadingspaces
        {
            get
            {
                return ShortBranchCodeRecipt.ToString().InsertLeadingSpaces(8);
            }
        }
        private string StoreUrl
        {
            get
            {
                return _config.StoreUrl;
            }
        }
        private string api_key
        {
            get
            {
                return _config.ApiKey;
            }
        }
        private string api_secret
        {
            get
            {
                return _config.ApiSecret;
            }
        }
        private string host
        {
            get
            {
                return _config.FtpHost;
            }
        }
        private string userName
        {
            get
            {
                return _config.FtpUserName;
            }
        }
        private string password
        {
            get
            {
                return _config.FtpPassword;
            }
        }
        private int port
        {
            get
            {
                return _config.FtpPort.GetValueOrDefault();
            }
        }
        private int InventoryImportEveryMinute
        {
            get
            {
                return _config.InventoryUpdateEveryMinute.GetValueOrDefault();
            }
        }
        private int DailyRecieptsHour
        {
            get
            {
                return _config.DailyRecieptsHour.GetValueOrDefault();
            }
        }
        private int DailyRecieptsMinute
        {
            get
            {
                return _config.DailyRecieptsMinute.GetValueOrDefault();
            }
        }
        private int DailySalesHour
        {
            get
            {
                return _config.DailySalesHour.GetValueOrDefault();
            }
        }
        private int DailySalesMinute
        {
            get
            {
                return _config.DailySalesMinute.GetValueOrDefault();
            }
        }
        private int DailyReportHour
        {
            get
            {
                return _config.DailyReportHour.GetValueOrDefault();
            }
        }
        private int DailyReportMinute
        {
            get
            {
                return _config.DailyReportMinute.GetValueOrDefault();
            }
        }
        private string ReportEmailAddress1
        {
            get
            {
                return _config.ReportEmailAddress1;
            }
        }
        private string ReportEmailAddress2
        {
            get
            {
                return _config.ReportEmailAddress2;
            }
        }
        private string smtpHost
        {
            get
            {
                return _config.SmtpHost;
            }
        }
        private int smtpPort
        {
            get
            {
                return _config.SmtpPort.GetValueOrDefault();
            }
        }
        private string emailUserName
        {
            get
            {
                return _config.SenderEmail;
            }
        }
        private string emailPassword
        {
            get
            {
                return _config.SenderemailPassword;
            }
        }
        private string displayName
        {
            get
            {
                return _config.DisplayName;
            }
        }
        private string toEmail
        {
            get
            {
                return _config.NotificationEmail;
            }
        }
        private string InvoiceFileName
        {
            get
            {
                return "invoices-web-" + DateTime.Now.ToString("yyMMdd") + ".dat";
            }
        }
        private string ReceiptsFileName
        {
            get
            {
                return "receipts-web-" + DateTime.Now.ToString("yyMMdd") + ".dat";
            }
        }
        private OrderService OrderServiceInstance
        {
            get
            {
                return new OrderService(StoreUrl, api_secret);
            }
        }
        private Dictionary<int, string> PaymentMeans
        {
            get
            {
                var means = new Dictionary<int, string>();
                var data = _context.PaymentMeans.ToList();
                foreach (var mean in data)
                {
                    means.Add(mean.Id, mean.Name);
                }
                return means;
            }
        }
        #endregion

        public IActionResult Index()
        {
            if (_config.UseRecurringJob.GetValueOrDefault())
            {
                IniateProcesses();
            }
            return View();
        }

        [HttpGet]
        public IActionResult IniateProcesses()
        {
            // hangfire
            var SalesCron = Cron.Daily(DailySalesHour, DailySalesMinute);
            var RecieptsCron = Cron.Daily(DailyRecieptsHour, DailyRecieptsMinute);
            var ReportsCron = Cron.Daily(DailyReportHour, DailyReportMinute);

            var minutes = InventoryImportEveryMinute == 0 ? 30 : InventoryImportEveryMinute;
            RecurringJob.AddOrUpdate(() => DoImoportAsync(), Cron.MinuteInterval(minutes), TimeZoneInfo.Local);
            RecurringJob.AddOrUpdate(() => ExportSalesAsync(false, default(DateTime), default(DateTime)), SalesCron, TimeZoneInfo.Local);
            RecurringJob.AddOrUpdate(() => ExportReceiptsAsync(false, default(DateTime), default(DateTime)), RecieptsCron, TimeZoneInfo.Local);
            RecurringJob.AddOrUpdate(() => ExportReportAsync(false, default(DateTime), default(DateTime), string.Empty), ReportsCron, TimeZoneInfo.Local);

            return Ok(new { valid = true });
        }

        #region Import Inventory CSV
        public ActionResult ImportInventoryUpdatesFromCSV()
        {
            return View();
        }

        #region Import from FTP
        [DisableConcurrentExecution(120)]

        public async Task DoImoportAsync()
        {
            bool importSuccess = false;

            FileInformation info = await ValidateInventoryUpdatesFromCSVAsync();

            if (info != null && !string.IsNullOrEmpty(info.fileName))
            {
                lock (importInventoryLock)
                {
                    var fileImportStatus = _context.FilesImportStatus.FirstOrDefault(s => s.FileName.Trim() == info.fileName.Trim());

                    if (fileImportStatus != null)
                    {
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

                string subject = info.fileName + " Import Status";

                if (info.isValid && info.lsErrorCount == 0)
                {
                    Utility.SendEmail(smtpHost, smtpPort, emailUserName, emailPassword, displayName, toEmail, $"Inventory update starting with the file {info.fileName}", "processing " + info.fileName + " has been satrted.");

                    var sucess = await ImportValidInvenotryUpdatesFromCSVAsync(info);
                    if (!sucess)
                    {
                        importSuccess = false;
                    }
                    else
                    {
                        importSuccess = true;
                    }
                }


                if (importSuccess)
                {
                    FtpHandler.DeleteFile(info.fileName, host, "/out", userName, password);
                    var body = messageBody("Import inventory File", "success", info.fileName + ".log");
                    Utility.SendEmail(smtpHost, smtpPort, emailUserName, emailPassword, displayName, toEmail, body, subject);
                }
                else
                {
                    var logFile = Encoding.ASCII.GetBytes(String.Join(Environment.NewLine, info.LsOfErrors.ToArray()));
                    FtpHandler.DeleteFile(info.fileName, host, "/out", userName, password);
                    var body = messageBody("Import inventory File", "failed", info.fileName + ".log");
                    Utility.SendEmail(smtpHost, smtpPort, emailUserName, emailPassword, displayName, toEmail, body, subject, logFile);
                }

                UpdateFileImportStatus(importSuccess, true, info);
            }
        }

        private void InsertFileImportStatus(bool importSuccess, bool isCompleted, FileInformation info)
        {
            try
            {
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
                FilesImportStatus importStatus = _context.FilesImportStatus.FirstOrDefault(f => f.FileName.Trim() == info.fileName.Trim());

                if (importStatus != null)
                {
                    importStatus.IsCompleted = isCompleted;
                    importStatus.IsImportSuccess = importSuccess;

                    _context.SaveChanges();
                }
            }
            catch (Exception e)
            {
                _log.Error(e.Message);
            }
        }

        public async Task<FileInformation> ValidateInventoryUpdatesFromCSVAsync()
        {

            List<string> LsOfSuccess = new List<string>();
            List<string> LsOfErrors = new List<string>();

            List<string> fileRows = new List<string>();

            FileInformation info = new FileInformation();
            bool validFile = false;

            info.fileName = "";

            try
            {
                var fileName = "";
                var fileContent = FtpHandler.ReadLatestFileFromFtp(host, userName, password, "/Out", out fileName);

                info.fileName = fileName;

                if (!string.IsNullOrEmpty(fileContent))
                {
                    var Rows = fileContent.Split(Environment.NewLine).ToArray(); // skip the header

                    var ProductServices = new ProductService(StoreUrl, api_secret);
                    var InventoryLevelsServices = new InventoryLevelService(StoreUrl, api_secret);

                    var Headers = Rows[0];
                    if (IsValidHeaders(Headers))
                    {
                        Rows = Rows.Last().Equals("") ? Rows.SkipLast(1).ToArray() : Rows;
                        Rows = Rows.Last().Contains("\0") ? Rows.SkipLast(1).ToArray() : Rows;

                        Rows = Rows.Skip(1).ToArray();// skip headers

                        fileRows = Rows.ToList();

                        int rowIndex = 1;

                        foreach (var row in Rows)
                        {
                            try
                            {
                                if (row.IsNotNullOrEmpty() && IsValidRow(row))
                                {
                                    var splittedRow = row.Split(',');
                                    string Handle = splittedRow[0];
                                    string Sku = splittedRow[1];
                                    string Method = splittedRow[2];
                                    string Quantity = splittedRow[3];

                                    var Products = await ProductServices.ListAsync(new ProductListFilter { Handle = Handle });
                                    var ProductObj = Products.Items.FirstOrDefault();

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
                                    Thread.Sleep(500);
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
            return info;
        }

        private async Task<bool> ImportValidInvenotryUpdatesFromCSVAsync(FileInformation info)
        {
            try
            {
                List<string> RowsWithoutHeader = info.fileRows;

                var ProductServices = new ProductService(StoreUrl, api_secret);
                var InventoryLevelsServices = new InventoryLevelService(StoreUrl, api_secret);

                info.LsOfSucess.Add("[Inventory] : file name : " + info.fileName + "--" + "discovered and will be processed, rows count: " + RowsWithoutHeader.Count);
                info.LsOfErrors.Add("[Inventory] : file name : " + info.fileName + "--" + "discovered and will be processed, rows count: " + RowsWithoutHeader.Count);

                foreach (var row in RowsWithoutHeader)
                {
                    var splittedRow = row.Split(',');

                    string Handle = splittedRow[0];
                    string Sku = splittedRow[1];
                    string Method = splittedRow[2];
                    string Quantity = splittedRow[3];

                    var Products = await ProductServices.ListAsync(new ProductListFilter { Handle = Handle });
                    var ProductObj = Products.Items.FirstOrDefault();
                    var VariantObj = ProductObj.Variants.FirstOrDefault(a => a.SKU == Sku);

                    var InventoryItemIds = new List<long>() { VariantObj.InventoryItemId.GetValueOrDefault() };
                    var InventoryItemId = new List<long>() { VariantObj.InventoryItemId.GetValueOrDefault() }.FirstOrDefault();

                    var LocationQuery = await InventoryLevelsServices.ListAsync(new InventoryLevelListFilter { InventoryItemIds = InventoryItemIds });
                    var LocationId = LocationQuery.Items.FirstOrDefault().LocationId;

                    Thread.Sleep(500);
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

                    _log.Info("the handle : " + Handle + "--" + "processed");

                    info.LsOfSucess.Add("the handle : " + Handle + "--" + "processed.");

                    Thread.Sleep(500);
                }
                _log.Info("file processed sucesfully");

                info.LsOfSucess.Add("file: " + info.fileName + "processed sucesfully");

                return true;
            }
            catch (Exception ex)
            {
                _log.Error("Error While Importing The File : " + ex.Message);
                info.LsOfErrors.Add("file: " + info.fileName + "processed failed,Error Message: " + ex.Message + "Error: " + ex.ToString());

                return false;
            }
        }
        #endregion

        #region Import using the website

        [HttpPost]
        public async Task<IActionResult> ImportInventoryUpdatesFromCSV(IFormFile File)
        {
            bool importSuccess = false;
            FileInformation info = await ValidateInventoryUpdatesFromCSVAsync(File);
            var fileName = Path.GetFileNameWithoutExtension(File.FileName);
            string subject = fileName + " Import Status";

            if (info != null)
            {
                if (info.isValid && info.lsErrorCount == 0)
                {
                    var errorCount = await ImportValidInvenotryUpdatesFromCSVAsync(File);
                    {
                        if (errorCount > 0)
                        {
                            importSuccess = false;
                        }
                        else
                        {
                            importSuccess = true;
                        }
                    }
                }
                else if (!info.isValid && info.lsErrorCount > 0)
                {
                    importSuccess = false;
                }

                if (importSuccess)
                {
                    var body = messageBody("Import inventory File", "Success", File.FileName);
                    Utility.SendEmail(smtpHost, smtpPort, emailUserName, emailPassword, displayName, toEmail, body, subject);
                }
                else
                {
                    var body = messageBody("Import inventory File", "Failed", File.FileName);
                    Utility.SendEmail(smtpHost, smtpPort, emailUserName, emailPassword, displayName, toEmail, body, subject);
                }
            }

            ImportCSVViewModel model = new ImportCSVViewModel();
            model.LsOfErrors = LsOfManualErrors;
            model.ErrorCount = LsOfManualErrors.Count;
            model.SucessCount = LsOfManualSuccess.Count;
            model.Validate = info.isValid;
            model.LsOfSucess = LsOfManualSuccess;

            return View(model);
        }

        public async Task<FileInformation> ValidateInventoryUpdatesFromCSVAsync(IFormFile File)
        {
            FileInformation info = new FileInformation();
            bool validFile = false;
            try
            {
                using (var reader = new StreamReader(File.OpenReadStream()))
                {
                    var FileContent = reader.ReadToEnd();

                    var Rows = FileContent.Split(Environment.NewLine).SkipLast(1).ToArray(); // skip the header

                    var ProductServices = new ProductService(StoreUrl, api_secret);
                    var InventoryLevelsServices = new InventoryLevelService(StoreUrl, api_secret);

                    var Headers = Rows[0];
                    if (IsValidHeaders(Headers))
                    {
                        Rows = Rows.Skip(1).ToArray();// skip headers
                        int rowIndex = 2; // first row in csv sheet is 2 (after header)
                        foreach (var row in Rows)
                        {
                            try
                            {
                                if (row.IsNotNullOrEmpty() && IsValidRow(row))
                                {
                                    var splittedRow = row.Split(',');
                                    string Handle = splittedRow[0];
                                    string Sku = splittedRow[1];
                                    string Method = splittedRow[2];
                                    string Quantity = splittedRow[3];

                                    var Products = await ProductServices.ListAsync(new ProductListFilter { Handle = Handle });
                                    var ProductObj = Products.Items.FirstOrDefault();

                                    if (ProductObj == null)
                                    {
                                        throw new Exception(string.Format("Product {0} not exists.", Handle, rowIndex));
                                    }
                                    var VariantObj = ProductObj.Variants.FirstOrDefault(a => a.SKU == Sku);
                                    if (VariantObj == null)
                                    {
                                        throw new Exception(string.Format("Variant {0} not exists.", Sku, rowIndex));
                                    }

                                    if (Method.ToLower().Trim() != "set" && Method.ToLower().Trim() != "in" && Method.ToLower().Trim() != "out")
                                    {
                                        throw new Exception(string.Format("Method {0} not defined.", Method, rowIndex));
                                    }

                                    Thread.Sleep(200);
                                }
                                else
                                {
                                    throw new Exception(string.Format("Empty or invalid row.", rowIndex));
                                }
                            }
                            catch (Exception ex)
                            {
                                LsOfManualErrors.Add("error occured in the row# " + rowIndex + " : " + ex.Message);
                            }
                            rowIndex++;
                        }
                    }
                    else
                    {
                        LsOfManualErrors.Add("Invalid csv file! ");
                    }

                    if (LsOfManualErrors != null && LsOfManualErrors.Count == 0)
                    {
                        validFile = true;
                    }
                }
            }
            catch (Exception ex)
            {
                LsOfManualErrors.Add("Error While Validating The File : " + ex.Message);
            }

            foreach (var ls in LsOfManualSuccess)
            {
                _log.Info(ls);
            }

            foreach (var ls in LsOfManualErrors)
            {
                _log.Error(ls);
            }
            info.LsOfSucess = LsOfManualSuccess;
            info.lsErrorCount = LsOfManualErrors.Count();
            info.isValid = validFile;
            return info;
        }

        private async Task<int> ImportValidInvenotryUpdatesFromCSVAsync(IFormFile File)
        {
            try
            {
                using (var reader = new StreamReader(File.OpenReadStream()))
                {
                    var FileContent = reader.ReadToEnd();
                    var Rows = FileContent.Split(Environment.NewLine).SkipLast(1).ToArray(); // skip the header

                    var ProductServices = new ProductService(StoreUrl, api_secret);
                    var InventoryLevelsServices = new InventoryLevelService(StoreUrl, api_secret);
                    Rows = Rows.Skip(1).ToArray();// skip headers
                    int rowIndex = 2; // first row in csv sheet is 2 (after header)
                    foreach (var row in Rows)
                    {
                        var splittedRow = row.Split(',');
                        string Handle = splittedRow[0];
                        string Sku = splittedRow[1];
                        string Method = splittedRow[2];
                        string Quantity = splittedRow[3];

                        var Products = await ProductServices.ListAsync(new ProductListFilter { Handle = Handle });
                        var ProductObj = Products.Items.FirstOrDefault();

                        var VariantObj = ProductObj.Variants.FirstOrDefault(a => a.SKU == Sku);

                        var InventoryItemIds = new List<long>() { VariantObj.InventoryItemId.GetValueOrDefault() };

                        var InventoryItemId = new List<long>() { VariantObj.InventoryItemId.GetValueOrDefault() }.FirstOrDefault();

                        var LocationQuery = await InventoryLevelsServices.ListAsync(new InventoryLevelListFilter { InventoryItemIds = InventoryItemIds });

                        var LocationId = LocationQuery.Items.FirstOrDefault().LocationId;

                        if (Method.ToLower().Trim() == "set")
                        {
                            var Result = await InventoryLevelsServices.SetAsync(new InventoryLevel { LocationId = LocationId, InventoryItemId = InventoryItemId, Available = Convert.ToInt32(Quantity) });
                            LsOfManualSuccess.Add(string.Format("Row# {0}-Inventory {1}.", rowIndex, "Updated"));
                        }
                        else if (Method.ToLower().Trim() == "in")
                        {
                            var Result = await InventoryLevelsServices.AdjustAsync(new InventoryLevelAdjust { LocationId = LocationId, InventoryItemId = InventoryItemId, AvailableAdjustment = Convert.ToInt32(Quantity) });
                            LsOfManualSuccess.Add(string.Format("Row# {0}-Inventory {1}.", rowIndex, "Updated"));
                        }
                        else if (Method.ToLower().Trim() == "out")
                        {
                            var Result = await InventoryLevelsServices.AdjustAsync(new InventoryLevelAdjust { LocationId = LocationId, InventoryItemId = InventoryItemId, AvailableAdjustment = Convert.ToInt32(Quantity) * -1 });
                            LsOfManualSuccess.Add(string.Format("Row# {0}-Inventory {1}.", rowIndex, "Updated"));
                        }
                        Thread.Sleep(200);
                        rowIndex++;
                    }
                }
            }
            catch (Exception ex)
            {
                LsOfManualErrors.Add("Error While Importing The File : " + ex.Message);
                _log.Error("Error While Importing The File : " + ex.Message);
            }
            return LsOfManualErrors.Count;
        }

        #endregion

        #region General functions

        private bool IsValidHeaders(string Headers)
        {
            var arr = Headers.Trim().ToLower().Split(",");

            return
                (arr[0].ToLower().Trim()).Contains("Product Handle".Trim().ToLower()) &&
                arr[1].ToLower().Trim().Contains("Variant SKU".Trim().ToLower()) &&
                arr[2].ToLower().Trim().Contains("Method".Trim().ToLower()) &&
                arr[3].ToLower().Trim().Contains("Quantity".Trim().ToLower());
        }
        private bool IsValidRow(string Row)
        {
            var arr = Row.Trim().ToLower().Split(",");
            return arr[0].IsNotNullOrEmpty() && arr[1].IsNotNullOrEmpty() && arr[2].IsNotNullOrEmpty() && arr[3].IsNotNullOrEmpty();
        }

        #endregion

        #endregion
        #region Export Daily Sales

        public ActionResult ExportDailySales()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> ExportSalesAsync(bool fromWeb, DateTime dateToRetriveFrom = default, DateTime dateToRetriveTo = default)
        {
            List<Order> lsOfOrders = new List<Order>();
            RefundedOrders refunded = new RefundedOrders();
            try
            {
                lsOfOrders = await GetNotExportedOrdersByPrefix("invoices", dateToRetriveFrom, dateToRetriveTo, lsOfOrders);
            }
            catch (ShopifyException e) when (e.Message.ToLower().Contains("exceeded 2 calls per second for api client") || (int)e.HttpStatusCode == 429 /* Too many requests */)
            {
                await Task.Delay(10000);

                lsOfOrders = await GetNotExportedOrdersByPrefix("invoices", dateToRetriveFrom, dateToRetriveTo, lsOfOrders);
            }

            try
            {
                await Task.Delay(1000);
                refunded = await GetRefundedOrdersAsync(dateToRetriveFrom, dateToRetriveTo);
            }
            catch (ShopifyException e) when (e.Message.ToLower().Contains("exceeded 2 calls per second for api client") || (int)e.HttpStatusCode == 429 /* Too many requests */)
            {
                await Task.Delay(10000);

                refunded = await GetRefundedOrdersAsync(dateToRetriveFrom, dateToRetriveTo);
            }

            if (refunded?.Orders?.Count > 0)
            {
                lsOfOrders.AddRange(refunded?.Orders);
            }

            lsOfOrders = lsOfOrders.OrderByDescending(a => a.CreatedAt.GetValueOrDefault().DateTime).ToList();
            string path = string.Empty;
            if (lsOfOrders.Count() > 0)
            {
                path = GenerateSalesFile(lsOfOrders, fromWeb);

                return View("~/Views/Home/ExportDailySales.cshtml", path);
            }
            else
            {
                var body = messageBody("Export Sales Invoices", "failed", "No orders!");
                return View("~/Views/Home/ExportDailySales.cshtml", "N/A");
            }
        }

        private string GenerateSalesFile(List<Order> orders, bool fromWeb, Dictionary<string, List<string>> lsOfTagTobeAdded = null)
        {
            var FileName = InvoiceFileName.Clone().ToString();
            var FolderDirectory = "/Data/invoices/";

            var path = _hostingEnvironment.WebRootPath + "/" + FolderDirectory + FileName;

            var ordersGroupedByDate = orders
        .GroupBy(o => o.CreatedAt.GetValueOrDefault().Date)
        .Select(g => new { OrdersDate = g.Key, Data = g.ToList() });

            using (FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(fileStream))
            {
                foreach (var DayOrders in ordersGroupedByDate)
                {
                    decimal taxPercentage = (decimal)_config.TaxPercentage;
                    var InvoiceDate = DayOrders.OrdersDate;

                    var BookNum = ShortBranchCodeSales + InvoiceDate.ToString("ddMMyy");

                    lock (salesFileLock)
                    {
                        file.WriteLine(
                       "0" +
                       "\t" + _customerCodeWithLeadingSpaces +
                       "\t" + InvoiceDate.ToString("dd/MM/y") + // order . creation , closed , processing date , invloice date must reagrding to payment please confirm.
                       "\t" + BookNum +
                       "\t" + "".InsertLeadingSpaces(4) + "\t" + WareHouseCode +
                       "\t" + ShortBranchCodeSales
                       );

                    }

                    foreach (var order in DayOrders.Data)
                    {
                        var shipRefOrder = order;
                        foreach (var orderItem in order.LineItems)
                        {

                            var discountPercentage = 0;

                            decimal? price;
                            decimal totalDiscount = 0;

                            //Calculate Discount on single lineItem
                            if (orderItem.DiscountAllocations != null && orderItem.DiscountAllocations.Count() != 0)
                            {
                                totalDiscount = orderItem.DiscountAllocations.Sum(a => decimal.Parse(a.Amount));
                            }

                            decimal totalWithVatPercentage = ((taxPercentage / 100.0m) + 1.0m);
                            decimal toBePerItem = orderItem.Quantity < 0 ? 1 : (decimal)orderItem.Quantity;
                            //Discounted Price without TAX and Discount
                            price = orderItem.Price.GetValueOrDefault() - Math.Round(totalDiscount / toBePerItem, 2);

                            if (orderItem.Taxable == false || order.TaxesIncluded == true)
                                price /= totalWithVatPercentage;

                            lock (salesFileLock)
                            {
                                file.WriteLine(
                                "1" + "\t" +
                                orderItem.SKU.InsertLeadingSpaces(15) + "\t" + // part number , need confirmation because max lenght is 15
                                orderItem.Quantity.ToString().InsertLeadingSpaces(10) + "\t" + // total quantity 
                                price.GetNumberWithDecimalPlaces(4).InsertLeadingSpaces(10) + "\t" + // unit price without tax
                                "".InsertLeadingSpaces(4) + "\t" + // agent code
                                discountPercentage.ToString("F") +
                                "\t" + "\t" + "\t" +
                                order.OrderNumber.GetValueOrDefault().ToString().InsertLeadingSpaces(24)
                                + "\t" +
                                order.CreatedAt.GetValueOrDefault().ToString("dd/MM/y HH:mm"));
                            }
                        }


                        var discountZero = 0;
                        var shipOrder = order;

                        var shippingAmount = (shipOrder.ShippingLines?.Sum(a => a.Price).GetValueOrDefault()).ValueWithoutTax();

                        //If the order (e.g partially/refunded or paid) 
                        //has shipping cost and this cost is not refunded,
                        //then write shipping data
                        if (shippingAmount > 0 && shipOrder.RefundKind != "refund_discrepancy")
                        {
                            var mQuant = "1";
                            if (shipOrder.RefundKind == "shipping_refund")
                            {
                                mQuant = "-1";
                                //Calculate refunded shipping
                                shippingAmount = Math.Abs(shipOrder.RefundAmount / ((taxPercentage / 100.0m) + 1.0m));
                            }

                            lock (salesFileLock)
                            {
                                file.WriteLine(
                                "1" + "\t" +
                                "921".InsertLeadingSpaces(15) + "\t" +
                                mQuant.ToString().InsertLeadingSpaces(10).InsertLeadingSpaces(10) + "\t" + // total quantity 
                                shippingAmount.GetNumberWithDecimalPlaces(4).InsertLeadingSpaces(10) + "\t" + // unit price without tax
                                "".InsertLeadingSpaces(4) + "\t" + // agent code
                                discountZero.ToString("F") +
                                "\t" + "\t" + "\t" +
                                order.OrderNumber.GetValueOrDefault().ToString().InsertLeadingSpaces(24)
                                + "\t" +
                                order.CreatedAt.GetValueOrDefault().ToString("dd/MM/y HH:mm"));
                            }
                        }

                        if (order.LineItems.Count() == 0 && shipOrder.RefundKind != "shipping_refund")
                        {
                            var mQuant = "-1";

                            var refundedAmount = Math.Abs(order.RefundAmount.ValueWithoutTax());

                            lock (salesFileLock)
                            {
                                file.WriteLine(
                                "1" + "\t" +
                                "925".InsertLeadingSpaces(15) + "\t" +
                                mQuant.ToString().InsertLeadingSpaces(10).InsertLeadingSpaces(10) + "\t" + // total quantity 
                                refundedAmount.GetNumberWithDecimalPlaces(4).InsertLeadingSpaces(10) + "\t" + // unit price without tax
                                "".InsertLeadingSpaces(4) + "\t" + // agent code
                                discountZero.ToString("F") +
                                "\t" + "\t" + "\t" +
                                order.OrderNumber.GetValueOrDefault().ToString().InsertLeadingSpaces(24)
                                + "\t" +
                                order.CreatedAt.GetValueOrDefault().ToString("dd/MM/y HH:mm"));
                            }
                        }
                    }
                }

                file.Close();
            }

            var FtpSuccesfully = true; // if web always true

            if (!fromWeb)
            {
                FtpSuccesfully = FtpHandler.UploadFile(FileName, System.IO.File.ReadAllBytes(path), host, "/In", userName, password);
                string subject = "Generate Sales File Status";
                var body = messageBody("Generate Sales File", "Success", "Invoices and Receipts/" + FileName);
                Utility.SendEmail(smtpHost, smtpPort, emailUserName, emailPassword, displayName, toEmail, body, subject);
            }

            if (FtpSuccesfully)
            {
                if (!fromWeb)
                {
                    _log.Info(FileName + "[sales] Uploaded sucesfully - the time is : " + DateTime.Now);
                }
            }
            else
            {
                _log.Error($"[sales] : Error during upload {FileName} to ftp");
                string subject = "Generate Sales File Status";
                var body = messageBody("Generate Sales File", "Failed", "Invoices and Receipts/" + FileName);
                Utility.SendEmail(smtpHost, smtpPort, emailUserName, emailPassword, displayName, toEmail, body, subject);
            }

            return FileName;
        }

        #endregion
        #region Export Daily Receipts

        public ActionResult ExportDailyReceipts()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> ExportReceiptsAsync(bool fromWeb, DateTime dateToRetriveFrom = default, DateTime dateToRetriveTo = default)
        {
            List<Order> lsOfOrders = new List<Order>();
            RefundedOrders refunded = new RefundedOrders();
            try
            {
                lsOfOrders = await GetNotExportedOrdersByPrefix("receipts", dateToRetriveFrom, dateToRetriveTo, lsOfOrders);
            }
            catch (ShopifyException e) when (e.Message.ToLower().Contains("exceeded 2 calls per second for api client") || (int)e.HttpStatusCode == 429 /* Too many requests */)
            {
                await Task.Delay(10000);

                lsOfOrders = await GetNotExportedOrdersByPrefix("receipts", dateToRetriveFrom, dateToRetriveTo, lsOfOrders);
            }

            try
            {
                await Task.Delay(1000);
                refunded = await GetRefundedOrdersAsync(dateToRetriveFrom, dateToRetriveTo);
            }
            catch (ShopifyException e) when (e.Message.ToLower().Contains("exceeded 2 calls per second for api client") || (int)e.HttpStatusCode == 429 /* Too many requests */)
            {
                await Task.Delay(10000);

                refunded = await GetRefundedOrdersAsync(dateToRetriveFrom, dateToRetriveTo);
            }

            if (refunded?.Orders?.Count > 0)
            {
                lsOfOrders.AddRange(refunded?.Orders);
            }

            lsOfOrders = lsOfOrders.OrderByDescending(a => a.CreatedAt.GetValueOrDefault().DateTime).ToList();
            string path = string.Empty;
            if (lsOfOrders.Count() > 0)
            {
                path = await GenerateReceiptFileAsync(lsOfOrders, fromWeb);
                return View("~/Views/Home/ExportDailyReceipts.cshtml", path);
            }
            else
            {
                var body = messageBody("Export Receipts", "failed", "No orders!");
                return View("~/Views/Home/ExportDailyReceipts.cshtml", "N/A");
            }
        }

        private async Task<string> GenerateReceiptFileAsync(List<Order> orders, bool fromWeb, Dictionary<string, List<string>> lsOfTagTobeAdded = null)
        {
            var FileName = ReceiptsFileName.Clone().ToString();
            var FolderDirectory = "/Data/receipts/";
            string path = _hostingEnvironment.WebRootPath + "/" + FolderDirectory + "/" + FileName;

            using (FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(fileStream))
            {
                foreach (var order in orders)
                {
                    Receipt transaction = null;
                    //Transactions
                    var index = orders.IndexOf(order);

                    // If this order is not the first, wait for .5 seconds (an average of 2 calls per second).
                    if (index > 0)
                    {
                        await Task.Delay(1000);
                    }

                    try
                    {
                        transaction = await GetTransactionByOrderAsync(order);
                    }
                    catch (ShopifyException e) when (e.Message.ToLower().Contains("exceeded 2 calls per second for api client") || (int)e.HttpStatusCode == 429 /* Too many requests */)
                    {
                        await Task.Delay(10000);

                        transaction = await GetTransactionByOrderAsync(order);
                    }

                    var InvoiceNumber = GetInvoiceNumber(order);
                    var priceWithoutTaxes = order.TotalPrice - order.TotalTax;
                    var priceWithTaxes = order.TotalPrice;

                    var invoiceDate = order.CreatedAt.GetValueOrDefault().ToString("dd/MM/yy");


                    var PaymentMeanCode = 0;
                    if (transaction != null)
                    {
                        PaymentMeanCode = GetPaymentMeanCode(transaction.cc_type);
                        if (transaction.x_timestamp.IsNotNullOrEmpty())
                        {
                            invoiceDate = Convert.ToDateTime(transaction.x_timestamp).ToString("dd/MM/yy");
                        }
                    }
                    else
                    {
                        PaymentMeanCode = 0;
                    }

                    //if it's a refund make it [minus] and [priceWithoutTaxes = priceWithTaxes]
                    if (order.RefundKind != "no_refund")
                    {
                        priceWithTaxes *= -1;
                        priceWithoutTaxes = priceWithTaxes;
                    }

                    lock (reciptsFileLock)
                    {
                        file.WriteLine(
                        "0" +  // spaces to fit indexes
                        " " + _customerCodeWithLeadingSpaces +
                        " " + invoiceDate + // order . creation , closed , processing date , invloice date must reagrding to payment please confirm.
                        " " + InvoiceNumber.InsertLeadingSpaces(13) + "".InsertLeadingSpaces(5) + // per indexes
                        " " + _shortBranchCodeReciptsWithLeadingspaces + "".InsertLeadingSpaces(18) +
                        " " + priceWithoutTaxes.GetNumberWithDecimalPlaces(2).InsertLeadingSpaces(13));

                        file.WriteLine(
                        "2" +
                        " " + PaymentMeanCode.ToString().InsertLeadingZeros(2) +
                        " " + priceWithTaxes.GetValueOrDefault().GetNumberWithDecimalPlaces(2).InsertLeadingSpaces(13) + // total payment amount Or Transaction.Amount
                        " " + "00" + //term code
                        " " + priceWithTaxes.GetValueOrDefault().GetNumberWithDecimalPlaces(2).InsertLeadingSpaces(13) + // first payment amount Or Transaction.Amount
                        " " + invoiceDate +
                        " " + "".InsertLeadingSpaces(8) +//card number
                        " " + "".InsertLeadingZeros(16));//Payment account
                    }
                }

                file.Close();
            }


            var FtpSuccesfully = true;

            if (!fromWeb)
            {

                FtpSuccesfully = FtpHandler.UploadFile(FileName, System.IO.File.ReadAllBytes(path), host, "/In", userName, password);
                string subject = "Generate Receipt File Status";
                var body = messageBody("Generate Receipt File", "Success", "Invoices and Receipts/" + FileName);
                Utility.SendEmail(smtpHost, smtpPort, emailUserName, emailPassword, displayName, toEmail, body, subject);
            }

            if (FtpSuccesfully)
            {
                if (!fromWeb)
                {
                    _log.Info(FileName + "[receipts] Uploaded sucesfully - the time is : " + DateTime.Now);
                }
            }
            else
            {
                _log.Error($"[receipts] : Error during upload {FileName} to ftp");

                string subject = "Generate Receipt File Status";
                var body = messageBody("Generate Receipt File", "Failed", "Invoices and Receipts/" + FileName);
                Utility.SendEmail(smtpHost, smtpPort, emailUserName, emailPassword, displayName, toEmail, body, subject);
            }

            return FileName;
        }

        private async Task<Receipt> GetTransactionByOrderAsync(Order order)
        {
            Receipt r = null;
            if (order.RefundKind == "no_refund" || !order.Transactions.Any())
            {
                var service = new TransactionService(StoreUrl, api_secret);
                var transactions = await service.ListAsync((long)order.Id);
                if (transactions.FirstOrDefault() != null)
                {

                    r = JsonConvert.DeserializeObject<Receipt>(transactions.FirstOrDefault().Receipt.ToString());

                    /*
                     * x_timestamp which is basically from the payment provider(Payplus) and it's inaccurate and wrong
                     * (it is in 12h UTC format and without AM or PM !)
                     * So transaction's created_at DateTime value used instead
                     */
                    r.x_timestamp = transactions.FirstOrDefault().CreatedAt.ToString();
                }
            }
            else
            {
                r = JsonConvert.DeserializeObject<Receipt>(
                    order.Transactions.Where(t => t.Kind.ToLower() == "refund").FirstOrDefault().Receipt.ToString());
                r.x_timestamp = order.Transactions.Where(t => t.Kind.ToLower() == "refund").FirstOrDefault().CreatedAt.ToString();
            }
            return r;
        }

        private string GetInvoiceNumber(Order order)
        {
            return order.OrderNumber.GetValueOrDefault().ToString();
        }

        private int GetPaymentMeanCode(string company)
        {
            if (company == null)
                return 0;

            var paymentMean = _context.PaymentMeans.Where(a => company.ToLower().Contains(a.Name.ToLower())).FirstOrDefault();
            if (paymentMean != null)
            {
                return paymentMean.Code.GetValueOrDefault();
            }
            else
            {
                return 0;
            }
        }

        #endregion
        #region Export Daily Report
        public ActionResult ExportDailyReport()
        {
            return View();
        }

        public async Task<ActionResult> ExportReportAsync(bool fromWeb, DateTime dateToRetriveFrom, DateTime dateToRetriveTo, string reportType = "")
        {
            List<Order> lsOfOrders = new List<Order>();
            RefundedOrders refunded = new RefundedOrders();
            FileModel file = new FileModel();

            bool isWorkingDay = CheckWorkingDays();
            if (!isWorkingDay && !fromWeb)
            {
                return View("~/Views/Home/ExportDailyReport.cshtml");
            }
            try
            {
                lsOfOrders = await GetReportOrdersByPrefix("receipts", dateToRetriveFrom, dateToRetriveTo, lsOfOrders);
            }
            catch (ShopifyException e) when (e.Message.ToLower().Contains("exceeded 2 calls per second for api client") || (int)e.HttpStatusCode == 429 /* Too many requests */)
            {
                await Task.Delay(10000);

                lsOfOrders = await GetReportOrdersByPrefix("receipts", dateToRetriveFrom, dateToRetriveTo, lsOfOrders);
            }

            try
            {
                await Task.Delay(1000);
                refunded = GetReportRefundedOrders(dateToRetriveFrom, dateToRetriveTo);
            }
            catch (ShopifyException e) when (e.Message.ToLower().Contains("exceeded 2 calls per second for api client") || (int)e.HttpStatusCode == 429 /* Too many requests */)
            {
                await Task.Delay(10000);

                refunded = GetReportRefundedOrders(dateToRetriveFrom, dateToRetriveTo);
            }

            if (refunded?.Orders?.Count > 0)
            {
                lsOfOrders.AddRange(refunded?.Orders);
            }

            lsOfOrders = lsOfOrders.OrderByDescending(a => a.CreatedAt.GetValueOrDefault().DateTime).ToList();

            if (lsOfOrders.Count > 0)
            {
                var contentType = "application/octet-stream";
                string extension = "xlsx";

                var products = await GetProductsAsync();

                await Task.Delay(1000);
                byte[] detailedFile = GenerateDetailedReportFile(lsOfOrders, products);
                string detailedFileName = $"DetailedReport{DateTime.Now.ToShortDateString()}.{extension}";

                await Task.Delay(1000);
                byte[] summarizedFile = GenerateSummarizedReportFile(lsOfOrders, products);
                string summarizedFileName = $"SummarizedReport{DateTime.Now.ToShortDateString()}.{extension}";

                file.DetailedFile = new FileContent()
                {
                    FileName = detailedFileName,
                    FileContentType = contentType,
                    FileData = detailedFile
                };

                file.SummarizedFile = new FileContent()
                {
                    FileName = summarizedFileName,
                    FileContentType = contentType,
                    FileData = summarizedFile
                };

                string subject = $"{ _config.SiteName} - Detailed And Summarized Report Files - {lsOfOrders.Count} Orders";
                string body = ReportEmailMessageBody();

                if (!string.IsNullOrEmpty(ReportEmailAddress1) || !string.IsNullOrEmpty(ReportEmailAddress2))
                {
                    Utility.SendReportEmail(smtpHost, smtpPort, emailUserName, emailPassword, displayName, ReportEmailAddress1, ReportEmailAddress2, body, subject, detailedFileName, detailedFile, summarizedFileName, summarizedFile);
                }
                else
                {
                    _log.Error("Email Addresses are Empty");
                }

                _log.Info($"[Daily Report] Generated and sent to : {ReportEmailAddress1} , {ReportEmailAddress2} sucesfully. File Names : {detailedFileName} , {summarizedFileName} - the time is : {DateTime.Now}");
            }
            else
            {
                if (!string.IsNullOrEmpty(ReportEmailAddress1) || !string.IsNullOrEmpty(ReportEmailAddress2))
                {
                    string subject = "Detailed And Summarized Report Files";
                    string body = NoOrdersEmailMessageBody();
                    Utility.SendEmail(smtpHost, smtpPort, emailUserName, emailPassword, displayName, ReportEmailAddress1, ReportEmailAddress2, body, subject);
                }
                else
                {
                    _log.Error("Email Addresses are Empty");
                }

                _log.Info($"No such orders");
            }

            return View("~/Views/Home/ExportDailyReport.cshtml", file);
        }

        private async Task<List<Order>> GetReportOrdersByPrefix(string prefix, DateTime dateToRetriveFrom, DateTime dateToRetriveTo, List<Order> lsOfOrders)
        {
            await Task.Delay(1000);
            //Date period Option
            if (dateToRetriveFrom != default && dateToRetriveTo != default)
            {
                lsOfOrders = GetReportOrders(prefix, dateToRetriveFrom, dateToRetriveTo);
            }
            //Single day Option
            else if (dateToRetriveFrom != default)
            {
                lsOfOrders = GetReportOrders(prefix, dateToRetriveFrom);

            }
            else if (dateToRetriveTo != default)
            {
                lsOfOrders = GetReportOrders(prefix, dateToRetriveFrom, dateToRetriveTo);

            }
            //Yesterday Option (Default)
            else
            {
                lsOfOrders = GetReportOrders(prefix);
            }

            return lsOfOrders;
        }

        private bool CheckWorkingDays()
        {
            var culture = new System.Globalization.CultureInfo("en-US");
            string currentDay = culture.DateTimeFormat.GetDayName(DateTime.Today.DayOfWeek);

            switch (currentDay)
            {
                case "Saturday":
                    return _config.Saturday ?? false;
                case "Sunday":
                    return _config.Sunday ?? false;
                case "Monday":
                    return _config.Monday ?? false;
                case "Tuesday":
                    return _config.Tuesday ?? false;
                case "Wednesday":
                    return _config.Wednesday ?? false;
                case "Thursday":
                    return _config.Thursday ?? false;
                case "Friday":
                    return _config.Friday ?? false;
                default:
                    return false;

            }
        }

        private byte[] GenerateSummarizedReportFile(List<Order> orders, List<Product> products)
        {
            var productsList = products;
            List<LineItem> lineItems = new List<LineItem>();
            List<SummarizedAutomaticReportModel> summarizedAutomaticReport = new List<SummarizedAutomaticReportModel>();

            foreach (var order in orders)
            {
                lineItems.AddRange(order.LineItems);
            }

            var lineItemsGroupedByVariantId = lineItems
            .Where(l => l.VariantId != null)
            .GroupBy(o => o.VariantId)
            .Select(g => new { VariantId = g.Key, Data = g.ToList() });

            foreach (var variantLineItems in lineItemsGroupedByVariantId)
            {
                string productVendor = string.Empty;
                string variantSKU = string.Empty;
                string productBarcode = string.Empty;

                int orderedQuantity = variantLineItems.Data.Count();

                var lineItem = variantLineItems.Data.FirstOrDefault();
                if (lineItem.ProductId != null)
                {
                    var productObj = productsList.FirstOrDefault(p => p.Id == lineItem.ProductId.Value);
                    productVendor = productObj.Vendor;
                    if (lineItem.VariantId != null)
                    {
                        variantSKU = productObj.Variants.Where(v => v.Id == lineItem.VariantId).Select(v => v.SKU).FirstOrDefault();
                        productBarcode = productObj.Variants.Where(v => v.Id == lineItem.VariantId).Select(v => v.Barcode).FirstOrDefault();
                    }
                }

                summarizedAutomaticReport.Add(new SummarizedAutomaticReportModel()
                {
                    ProductVendor = !string.IsNullOrEmpty(productVendor) ? productVendor : !string.IsNullOrEmpty(lineItem.Vendor) ? lineItem.Vendor : "N/A",
                    VariantSKU = !string.IsNullOrEmpty(variantSKU) ? variantSKU : !string.IsNullOrEmpty(lineItem.SKU) ? lineItem.SKU : "N/A",
                    ProductBarcode = !string.IsNullOrEmpty(productBarcode) ? productBarcode : "N/A",
                    OrderedQuantity = orderedQuantity
                });
            }

            summarizedAutomaticReport = summarizedAutomaticReport.OrderBy(r => r.ProductVendor).ThenBy(r => r.VariantSKU).ToList();

            string extension = "xlsx";

            try
            {
                List<List<SummarizedAutomaticReportModel>> splittedData = Utility.Split(summarizedAutomaticReport, 1000000);
                List<byte> data = new List<byte>();
                foreach (var summarizedReportModel in splittedData)
                {
                    var result = Utility.ExportToExcel(summarizedReportModel, extension).ToList();
                    data.AddRange(result);
                }

                var fileResult = data.ToArray();

                return fileResult;
            }
            catch (Exception e)
            {
                _log.Error(e.Message);
                throw e;
            }
        }

        private byte[] GenerateDetailedReportFile(List<Order> orders, List<Product> products)
        {
            var productsList = products;

            List<DetailedAutomaticReportModel> detailedAutomaticReport = new List<DetailedAutomaticReportModel>();
            foreach (var order in orders)
            {
                var localDetailReportList = new List<DetailedAutomaticReportModel>();
                string customerName = $"{order.Customer?.FirstName} {order.Customer?.LastName}";

                foreach (var lineItem in order.LineItems)
                {
                    string productVendor = string.Empty;
                    string variantSKU = string.Empty;
                    string productBarcode = string.Empty;

                    if (lineItem.ProductId != null)
                    {
                        var productObj = productsList.FirstOrDefault(p => p.Id == lineItem.ProductId.Value);
                        productVendor = productObj.Vendor;
                        if (lineItem.VariantId != null)
                        {
                            variantSKU = productObj.Variants.Where(v => v.Id == lineItem.VariantId).Select(v => v.SKU).FirstOrDefault();
                            productBarcode = productObj.Variants.Where(v => v.Id == lineItem.VariantId).Select(v => v.Barcode).FirstOrDefault();
                        }
                    }

                    var detailedReportModel = new DetailedAutomaticReportModel()
                    {
                        OrderName = order.Name,
                        CustomerName = !string.IsNullOrWhiteSpace(customerName) ? customerName : "N/A",
                        OrderDay = order.CreatedAt.Value.ToString("dd/MM/yyyy"),
                        ProductVendor = !string.IsNullOrWhiteSpace(productVendor) ? productVendor : !string.IsNullOrWhiteSpace(lineItem.Vendor) ? lineItem.Vendor : "N/A",
                        VariantSKU = !string.IsNullOrWhiteSpace(variantSKU) ? variantSKU : !string.IsNullOrWhiteSpace(lineItem.SKU) ? lineItem.SKU : "N/A",
                        OrderedQuantity = lineItem.Quantity.Value,
                        ProductBarcode = !string.IsNullOrWhiteSpace(productBarcode) ? productBarcode : "N/A"
                    };

                    localDetailReportList.Add(detailedReportModel);
                }

                localDetailReportList = localDetailReportList.OrderBy(r => GetOrderId(r.OrderName)).ThenBy(r => r.ProductVendor).ThenBy(r => r.VariantSKU).ToList();
                localDetailReportList.FirstOrDefault().CustomerNotes = order.Note;
                detailedAutomaticReport.AddRange(localDetailReportList);
            }

            detailedAutomaticReport = detailedAutomaticReport.OrderBy(r => GetOrderId(r.OrderName)).ThenBy(r => r.ProductVendor).ThenBy(r => r.VariantSKU).ToList();

            string extension = "xlsx";

            try
            {
                List<List<DetailedAutomaticReportModel>> splittedData = Utility.Split(detailedAutomaticReport, 1000000);
                List<byte> data = new List<byte>();
                foreach (var detailedReportModel in splittedData)
                {
                    var result = Utility.ExportToExcel(detailedReportModel, extension).ToList();
                    data.AddRange(result);
                }

                var fileResult = data.ToArray();

                return fileResult;
            }
            catch (Exception e)
            {
                _log.Error(e.Message);
                throw e;
            }
        }

        private List<Order> GetReportOrders(string prefix, DateTime dateFrom = default, DateTime dateTo = default)
        {
            dateFrom = dateFrom.Date;
            dateTo = dateTo.Date;

            OrderListFilter filter = new OrderListFilter();

            if (dateFrom != default && dateTo == default)
            {
                filter = new OrderListFilter
                {
                    FinancialStatus = "paid",
                    Status = "open",
                    FulfillmentStatus = "any",
                    CreatedAtMin = dateFrom.AbsoluteStart()
                };
            }
            else if (dateFrom == default && dateTo != default)
            {
                filter = new OrderListFilter
                {
                    FinancialStatus = "paid",
                    Status = "open",
                    FulfillmentStatus = "any",
                    CreatedAtMax = dateTo.AbsoluteEnd()
                };
            }
            else if (dateFrom == default && dateTo == default)
            {
                filter = new OrderListFilter
                {
                    FinancialStatus = "paid",
                    Status = "open",
                    FulfillmentStatus = "any"
                };
            }
            else
            {
                filter = new OrderListFilter
                {
                    FinancialStatus = "paid",
                    Status = "open",
                    FulfillmentStatus = "any",
                    CreatedAtMin = dateFrom.AbsoluteStart(),
                    CreatedAtMax = dateTo.AbsoluteEnd()
                };

            }

            List<Order> orders = GetOrderByFiltersAsync(filter).Result.Select(a => a).Where(a => a.FulfillmentStatus == null ||
                    a.FulfillmentStatus == "partial").ToList();

            return orders;
        }

        private RefundedOrders GetReportRefundedOrders(DateTime dateFrom = default, DateTime dateTo = default)
        {
            var refundedOrders = new RefundedOrders();

            dateFrom = dateFrom.Date;
            dateTo = dateTo.Date;

            OrderListFilter filter = new OrderListFilter();

            if (dateFrom != default && dateTo == default)
            {
                filter = new OrderListFilter
                {
                    FinancialStatus = "partially_refunded",
                    Status = "open",
                    FulfillmentStatus = "any",
                    CreatedAtMin = dateFrom.AbsoluteStart()
                };
            }
            else if (dateFrom == default && dateTo != default)
            {
                filter = new OrderListFilter
                {
                    FinancialStatus = "partially_refunded",
                    Status = "open",
                    FulfillmentStatus = "any",
                    CreatedAtMax = dateTo.AbsoluteEnd()
                };
            }
            else if (dateFrom == default && dateTo == default)
            {
                filter = new OrderListFilter
                {
                    FinancialStatus = "partially_refunded",
                    Status = "open",
                    FulfillmentStatus = "any"
                };
            }
            else
            {
                filter = new OrderListFilter
                {
                    FinancialStatus = "partially_refunded",
                    Status = "open",
                    FulfillmentStatus = "any",
                    CreatedAtMin = dateFrom.AbsoluteStart(),
                    CreatedAtMax = dateTo.AbsoluteEnd()
                };

            }

            List<Order> orders = GetOrderByFiltersAsync(filter).Result.Select(a => a).Where(a => a.FulfillmentStatus == null || a.FulfillmentStatus == "partial").ToList();

            var OrdersHasRefunds = orders.Where(a => a.Refunds.Count() > 0);
            foreach (var order in OrdersHasRefunds)
            {
                List<long> lineItemsIds = new List<long>();
                foreach (var refund in order.Refunds)
                {
                    var refundLineItems = refund.RefundLineItems;
                    foreach (var r in refundLineItems)
                    {
                        lineItemsIds.Add(r.LineItem.Id.GetValueOrDefault());
                    }
                }
                order.LineItems = order.LineItems.Where(l => !lineItemsIds.Contains(l.Id.GetValueOrDefault())).ToList();
            }

            var returnOrders = OrdersHasRefunds.Where(o => o.LineItems.Any()).ToList();
            refundedOrders.Orders = returnOrders;
            return refundedOrders;
        }

        private int GetOrderId(string orderName)
        {
            var orderSplitted = orderName.Split('#');
            var order = int.Parse(orderSplitted[1]);
            return order;
        }

        [HttpPost]
        public FileResult DownloadReport(string fileData, string contentType, string fileName)
        {
            return File(System.Convert.FromBase64String(fileData), contentType, fileName);
        }

        public async Task<List<Product>> GetProductsAsync()
        {
            List<Product> products = new List<Product>();

            var productServices = new ProductService(StoreUrl, api_secret);

            var filter = new ProductListFilter
            {
                Limit = 250,
                Fields = "id,handle,vendor,Variants"
            };

            var page = await productServices.ListAsync(filter);

            while (true)
            {
                products.AddRange(page.Items);

                if (!page.HasNextPage)
                {
                    break;
                }

                try
                {
                    page = await productServices.ListAsync(page.GetNextPageFilter());
                }
                catch (ShopifyRateLimitException e)
                {
                    await Task.Delay(10000);

                    page = await productServices.ListAsync(page.GetNextPageFilter());
                }
            }

            return products;
        }

        private string ReportEmailMessageBody()
        {
            string body = "Hi - <br /><br /> Detailed and Summraized Report Files Generated. <br />";
            body += "Please Find them in the attachments <br /><br />";
            body += "Thank you<br />";
            body += "Gottex";

            return body;
        }

        private string NoOrdersEmailMessageBody()
        {
            string body = "Hi - <br /><br /> ";
            body += "No Such Orders To FulFill <br /><br />";
            body += "Thank you<br />";
            body += "Gottex";

            return body;
        }

        #endregion
        #region General Use

        private async Task<List<Order>> GetNotExportedOrdersByPrefix(string prefix, DateTime dateToRetriveFrom, DateTime dateToRetriveTo, List<Order> lsOfOrders)
        {
            if (lsOfOrders is null)
            {
                lsOfOrders = new List<Order>();
            }

            await Task.Delay(1000);
            //Date period option
            if (dateToRetriveFrom != default && dateToRetriveTo != default)
            {
                lsOfOrders = await GetNotExportedOrdersAsync(prefix, dateToRetriveFrom, dateToRetriveTo);
            }
            //Single day option
            else if (dateToRetriveFrom != default)
            {
                lsOfOrders = await GetNotExportedOrdersAsync(prefix, dateToRetriveFrom);
            }
            else if (dateToRetriveTo != default)
            {
                lsOfOrders = await GetNotExportedOrdersAsync(prefix, dateToRetriveFrom, dateToRetriveTo);
            }
            //Yesterday option (Default)
            else
            {
                lsOfOrders = await GetNotExportedOrdersAsync(prefix);
            }

            return lsOfOrders;
        }

        private async Task<List<Order>> GetNotExportedOrdersAsync(string prefix, DateTime dateFrom = default, DateTime dateTo = default)
        {
            if (dateFrom == default) //Yesterday option (Default)
            {
                dateFrom = DateTime.Now.AddDays(-1); // by default
                dateTo = DateTime.Now.AddDays(-1);
            }
            else if (dateTo == default) //Single day option
            {
                dateTo = dateFrom.Date;
            }

            // to trim hours and minutes, ...
            dateFrom = dateFrom.Date;
            dateTo = dateTo.Date;

            var filter = new OrderListFilter
            {
                FinancialStatus = "any",
                Status = "any",
                FulfillmentStatus = "any",
                CreatedAtMin = dateFrom.AbsoluteStart(),
                CreatedAtMax = dateTo.AbsoluteEnd()
            };

            List<Order> orders = await GetNotExportedOrderByFiltersAsync(filter);

            return orders;
        }

        private async Task<List<Order>> GetNotExportedOrderByFiltersAsync(OrderListFilter filter)
        {
            List<Order> Orders = new List<Order>();

            var paidFinancialFilter = new OrderListFilter
            {
                FinancialStatus = "paid",
                Status = filter.Status,
                FulfillmentStatus = filter.FulfillmentStatus,
                CreatedAtMin = filter.CreatedAtMin,
                CreatedAtMax = filter.CreatedAtMax
            };

            var refundedFinancialFilter = new OrderListFilter
            {
                FinancialStatus = "refunded",
                Status = filter.Status,
                FulfillmentStatus = filter.FulfillmentStatus,
                CreatedAtMin = filter.CreatedAtMin,
                CreatedAtMax = filter.CreatedAtMax
            };

            var partiallyRefundedFinancialFilter = new OrderListFilter
            {
                FinancialStatus = "partially_refunded",
                Status = filter.Status,
                FulfillmentStatus = filter.FulfillmentStatus,
                CreatedAtMin = filter.CreatedAtMin,
                CreatedAtMax = filter.CreatedAtMax
            };

            Orders.AddRange(await GetOrderByFiltersAsync(paidFinancialFilter));
            Orders.AddRange(await GetOrderByFiltersAsync(refundedFinancialFilter));
            Orders.AddRange(await GetOrderByFiltersAsync(partiallyRefundedFinancialFilter));

            return Orders;
        }

        private async Task<RefundedOrders> GetRefundedOrdersAsync(DateTime dateFrom = default, DateTime dateTo = default)
        {
            var refundedOrders = new RefundedOrders();

            Dictionary<string, List<string>> lsOfTagsToBeAddedTemp = new Dictionary<string, List<string>>();

            if (dateFrom == default) //Yesterday option (Default)
            {
                dateFrom = DateTime.Now.AddDays(-1); // by default
                dateTo = DateTime.Now.AddDays(-1);
            }
            else if (dateTo == default) //Single day option
            {
                dateTo = dateFrom.Date;
            }

            // to trim hours and minutes, ...
            dateFrom = dateFrom.Date;
            dateTo = dateTo.Date;

            List<Order> orders = await GetRefundedOrdersByFiltersAsync();

            var OrdersHasRefunds = orders.Where(a => a.Refunds.Count() > 0);
            var ordersToReturn = new List<Order>();
            decimal taxPercentage = (decimal)_config.TaxPercentage;

            foreach (var order in OrdersHasRefunds)
            {
                var targetRefunds = order.Refunds.Where(a => a.CreatedAt.GetValueOrDefault().Date >= dateFrom.AbsoluteStart() &&
                a.CreatedAt.GetValueOrDefault().Date <= dateTo.AbsoluteEnd()).ToList();

                foreach (var refund in targetRefunds)
                {
                    var orderToReturn = new Order
                    {
                        TotalDiscounts = order.TotalDiscounts,
                        OrderNumber = order.OrderNumber,
                        Id = order.Id,
                        Tags = order.Tags,

                        SubtotalPrice = order.SubtotalPrice,
                        FinancialStatus = order.FinancialStatus,
                        ShippingLines = order.ShippingLines
                    };

                    var refundLineItems = refund.RefundLineItems;

                    List<LineItem> lsOfLineItems = new List<LineItem>();

                    foreach (var itemRefund in refundLineItems)
                    {
                        lsOfLineItems.Add(new LineItem
                        {
                            Quantity = itemRefund.Quantity * -1,
                            Price = itemRefund.LineItem.Price,
                            SKU = itemRefund.LineItem.SKU,
                            Taxable = itemRefund.LineItem.Taxable,
                            Id = itemRefund.LineItem.Id,
                            DiscountAllocations = itemRefund.LineItem.DiscountAllocations,
                        });
                        foreach (var discount in lsOfLineItems.Last().DiscountAllocations)
                        {
                            List<LineItem> tt = order.LineItems.Where(a => a.Id == lsOfLineItems.Last().Id).ToList();
                            decimal quantity = (decimal)tt.First().Quantity;
                            discount.Amount = (decimal.Parse(discount.Amount) / quantity)
                                + "";
                        }
                    }

                    orderToReturn.CreatedAt = refund.CreatedAt;

                    orderToReturn.TaxesIncluded = false;

                    orderToReturn.LineItems = lsOfLineItems;

                    orderToReturn.TaxLines = order.TaxLines;

                    orderToReturn.TaxesIncluded = order.TaxesIncluded;

                    orderToReturn.Transactions = refund.Transactions;

                    var totalPrice = refund.Transactions.Sum(t => t.Amount);
                    decimal priceWithVat = (decimal)totalPrice / ((taxPercentage / 100.0m) + 1.0m);

                    orderToReturn.TotalTax = totalPrice - priceWithVat;
                    orderToReturn.TotalPrice = totalPrice;

                    var refundInfo = refund.OrderAdjustments;

                    orderToReturn.RefundKind = "refund_discrepancy";

                    if (refundInfo != null && refundInfo.Count() != 0)
                    {
                        orderToReturn.RefundAmount = (decimal)((refund.OrderAdjustments.First().Amount +
                                    refund.OrderAdjustments.First().TaxAmount));
                        orderToReturn.RefundKind = refund.OrderAdjustments.First().Kind;
                    }

                    ordersToReturn.Add(orderToReturn);
                }
            }
            refundedOrders.Orders = ordersToReturn.ToList();

            return refundedOrders;
        }

        public async Task<List<Order>> GetOrderByFiltersAsync(OrderListFilter filter)
        {
            List<Order> Orders = new List<Order>();

            var orderService = new OrderService(StoreUrl, api_secret);
            filter.Limit = 250;

            var page = await orderService.ListAsync(filter);

            while (true)
            {
                Orders.AddRange(page.Items);

                if (!page.HasNextPage)
                {
                    break;
                }

                try
                {
                    page = await orderService.ListAsync(page.GetNextPageFilter());
                }
                catch (ShopifyRateLimitException e)
                {
                    await Task.Delay(10000);

                    page = await orderService.ListAsync(page.GetNextPageFilter());
                }
            }

            return Orders;
        }


        public async Task<List<Order>> GetRefundedOrdersByFiltersAsync()
        {
            List<Order> Orders = new List<Order>();

            var refundedFilter = new OrderListFilter
            {
                FinancialStatus = "refunded",
                Status = "any",
                FulfillmentStatus = "any"
            };

            var partiallyRefundedFilter = new OrderListFilter
            {
                FinancialStatus = "partially_refunded",
                Status = "any",
                FulfillmentStatus = "any"
            };

            Orders.AddRange(await GetOrderByFiltersAsync(refundedFilter));
            Orders.AddRange(await GetOrderByFiltersAsync(partiallyRefundedFilter));

            return Orders;
        }

        private string messageBody(string operationName, string status, string fileName)
        {
            string body = "Hi - <br /><br />Operation: " + operationName + "<br />";
            body += "Status: " + status + "<br />";
            body += "Log File Location: " + fileName + "<br /><br />";
            body += "Thank you<br />";
            body += "Gottex";

            return body;
        }

        public FileResult DownloadFile(string fileToDownload, string subFolder)
        {
            string filePath = _hostingEnvironment.WebRootPath + $"/{subFolder}/{fileToDownload}";
            string fileName = fileToDownload;

            byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);

            return File(fileBytes, "application/force-download", fileName);
        }
        #endregion
    }
}