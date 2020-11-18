using Log4NetLibrary;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using ShopifyApp2;
using ShopifyApp2.ViewModel;
using ShopifySharp;
using ShopifySharp.Filters;
using SyncApp.Helpers;
using SyncApp.Models;
using SyncApp.Models.EF;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SyncApp.Logic
{
    public class ImportInventoryWebLogic
    {
        private static readonly log4net.ILog _log = Logger.GetLogger();
        private readonly ShopifyAppContext _context;

        List<string> LsOfManualSuccess = new List<string>();
        List<string> LsOfManualErrors = new List<string>();

        public ImportInventoryWebLogic(ShopifyAppContext context)
        {
            _context = context;
        }

        private Configrations _config
        {
            get
            {
                return _context.Configrations.First();
            }
        }

        #region prop
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
        private string StoreUrl
        {
            get
            {
                return _config.StoreUrl;
            }
        }
        private string api_secret
        {
            get
            {
                return _config.ApiSecret;
            }
        }
        #endregion

        public async Task<ImportCSVViewModel> ImportInventoryFileAsync(IFormFile File)
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
                    var body = EmailMessages.messageBody("Import inventory File", "Success", File.FileName);
                    Utility.SendEmail(smtpHost, smtpPort, emailUserName, emailPassword, displayName, toEmail, body, subject);
                }
                else
                {
                    var body = EmailMessages.messageBody("Import inventory File", "Failed", File.FileName);
                    Utility.SendEmail(smtpHost, smtpPort, emailUserName, emailPassword, displayName, toEmail, body, subject);
                }
            }

            ImportCSVViewModel model = new ImportCSVViewModel();
            model.LsOfErrors = LsOfManualErrors;
            model.ErrorCount = LsOfManualErrors.Count;
            model.SucessCount = LsOfManualSuccess.Count;
            model.Validate = info.isValid;
            model.LsOfSucess = LsOfManualSuccess;
            return model;
        }
        private async Task<FileInformation> ValidateInventoryUpdatesFromCSVAsync(IFormFile File)
        {
            FileInformation info = new FileInformation();
            bool validFile = false;
            try
            {
                using (var reader = new StreamReader(File.OpenReadStream()))
                {
                    var FileContent = reader.ReadToEnd();

                    var Rows = FileContent.Split(Environment.NewLine).SkipLast(1).ToArray(); // skip the header

                    var InventoryLevelsServices = new InventoryLevelService(StoreUrl, api_secret);

                    var Headers = Rows[0];
                    if (ValidateCSV.IsValidHeaders(Headers))
                    {
                        Rows = Rows.Skip(1).ToArray();// skip headers
                        int rowIndex = 2; // first row in csv sheet is 2 (after header)

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

                    var InventoryLevelsServices = new InventoryLevelService(StoreUrl, api_secret);
                    Rows = Rows.Skip(1).ToArray();// skip headers
                    int rowIndex = 2; // first row in csv sheet is 2 (after header)

                    var Products = await GetProductsAsync();

                    foreach (var row in Rows)
                    {
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
        public async Task<List<Product>> GetProductsAsync()
        {
            return await new GetShopifyProducts(StoreUrl, api_secret).GetProductsAsync();
        }
    }
}