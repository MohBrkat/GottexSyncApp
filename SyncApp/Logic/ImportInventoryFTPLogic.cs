﻿using Log4NetLibrary;
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

        public ImportInventoryFTPLogic(ShopifyAppContext context)
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


        public async Task ImportInventoryFileAsync()
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
                    var body = EmailMessages.messageBody("Import inventory File", "success", info.fileName + ".log");
                    Utility.SendEmail(smtpHost, smtpPort, emailUserName, emailPassword, displayName, toEmail, body, subject);
                }
                else
                {
                    var logFile = Encoding.ASCII.GetBytes(String.Join(Environment.NewLine, info.LsOfErrors.ToArray()));
                    FtpHandler.DeleteFile(info.fileName, host, "/out", userName, password);
                    var body = EmailMessages.messageBody("Import inventory File", "failed", info.fileName + ".log");
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

                var Products = await GetProductsAsync();

                foreach (var row in RowsWithoutHeader)
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
        public async Task<List<Product>> GetProductsAsync()
        {
            return await new GetShopifyProducts(StoreUrl, api_secret).GetProductsAsync();
        }
    }
}