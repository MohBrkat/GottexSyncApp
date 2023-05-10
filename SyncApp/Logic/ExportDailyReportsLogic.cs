using Log4NetLibrary;
using ShopifySharp;
using SyncAppEntities.Models;
using SyncAppEntities.Models.EF;
using SyncAppEntities.ViewModel;
using SyncAppCommon;
using SyncAppCommon.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncAppEntities.Logic
{
    public class ExportDailyReportsLogic
    {
        private static readonly log4net.ILog _log = Logger.GetLogger();
        private readonly ShopifyAppContext _context;

        public ExportDailyReportsLogic(ShopifyAppContext context)
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
        private string ReportEmailAddress1
        {
            get
            {
                return Config.ReportEmailAddress1 ?? string.Empty;
            }
        }
        private string ReportEmailAddress2
        {
            get
            {
                return Config.ReportEmailAddress2 ?? string.Empty;
            }
        }
        #endregion

        public async Task<List<Order>> ExportDailyReportsAsync(DateTime dateToRetriveFrom, DateTime dateToRetriveTo)
        {
            List<Order> lsOfOrders = new List<Order>();
            RefundedOrders refunded = new RefundedOrders();
            try
            {
                lsOfOrders = new GetShopifyOrders(StoreUrl, ApiSecret, _context).GetReportOrders(dateToRetriveFrom, dateToRetriveTo);
            }
            catch (ShopifyException e) when (e.Message.ToLower().Contains("exceeded 2 calls per second for api client") || (int)e.HttpStatusCode == 429 /* Too many requests */)
            {
                await Task.Delay(10000);

                lsOfOrders = new GetShopifyOrders(StoreUrl, ApiSecret, _context).GetReportOrders(dateToRetriveFrom, dateToRetriveTo);
            }

            try
            {
                await Task.Delay(1000);
                refunded = new GetShopifyOrders(StoreUrl, ApiSecret, _context).GetReportRefundedOrders(dateToRetriveFrom, dateToRetriveTo);
            }
            catch (ShopifyException e) when (e.Message.ToLower().Contains("exceeded 2 calls per second for api client") || (int)e.HttpStatusCode == 429 /* Too many requests */)
            {
                await Task.Delay(10000);

                refunded = new GetShopifyOrders(StoreUrl, ApiSecret, _context).GetReportRefundedOrders(dateToRetriveFrom, dateToRetriveTo);
            }

            if (refunded?.Orders?.Count > 0)
            {
                lsOfOrders.AddRange(refunded?.Orders);
            }

            lsOfOrders = lsOfOrders.OrderByDescending(a => a.CreatedAt.GetValueOrDefault().DateTime).ToList();
            return lsOfOrders;
        }

        public async Task GenerateDailyReportFilesAsync(FileModel file, List<Order> lsOfOrders)
        {
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

                string subject = $"{Config.SiteName} - Detailed And Summarized Report Files - {lsOfOrders.Count} Orders";
                string body = EmailMessages.ReportEmailMessageBody();

                if (!string.IsNullOrEmpty(ReportEmailAddress1) || !string.IsNullOrEmpty(ReportEmailAddress2))
                {
                    Utility.SendReportEmail(SmtpHost, SmtpPort, EmailUserName, EmailPassword, DisplayName, ReportEmailAddress1, ReportEmailAddress2, body, subject, detailedFileName, detailedFile, summarizedFileName, summarizedFile);
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
                    string body = EmailMessages.NoOrdersEmailMessageBody();
                    Utility.SendEmail(SmtpHost, SmtpPort, EmailUserName, EmailPassword, DisplayName, ReportEmailAddress1, ReportEmailAddress2, body, subject);
                }
                else
                {
                    _log.Error("Email Addresses are Empty");
                }

                _log.Info($"No such orders");
            }
        }

        public byte[] GenerateDetailedReportFile(List<Order> orders, List<Product> products)
        {
            var productsList = products;

            List<DetailedAutomaticReportModel> detailedAutomaticReport = new List<DetailedAutomaticReportModel>();
            foreach (var order in orders)
            {
                var localDetailReportList = new List<DetailedAutomaticReportModel>();
                string customerName = $"{order.Customer?.FirstName} {order.Customer?.LastName}";

                string shipping = string.Empty;
                if (order.ShippingLines.Any())
                {
                    var shippingLine = order.ShippingLines.First();
                    shipping = shippingLine.Code;
                }

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
                        ProductBarcode = !string.IsNullOrWhiteSpace(productBarcode) ? productBarcode : "N/A",
                        Shipping = shipping
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

        public byte[] GenerateSummarizedReportFile(List<Order> orders, List<Product> products)
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

        private int GetOrderId(string orderName)
        {
            var orderSplitted = orderName.Split('#');
            var order = int.Parse(orderSplitted[1]);
            return order;
        }

        public async Task<List<Product>> GetProductsAsync()
        {
            return await new GetShopifyProducts(StoreUrl, ApiSecret).GetProductsAsync();
        }

        public bool CheckWorkingDays()
        {
            var culture = new System.Globalization.CultureInfo("en-US");
            string currentDay = culture.DateTimeFormat.GetDayName(DateTime.Today.DayOfWeek);

            switch (currentDay)
            {
                case "Saturday":
                    return Config.Saturday ?? false;
                case "Sunday":
                    return Config.Sunday ?? false;
                case "Monday":
                    return Config.Monday ?? false;
                case "Tuesday":
                    return Config.Tuesday ?? false;
                case "Wednesday":
                    return Config.Wednesday ?? false;
                case "Thursday":
                    return Config.Thursday ?? false;
                case "Friday":
                    return Config.Friday ?? false;
                default:
                    return false;

            }
        }

    }
}
