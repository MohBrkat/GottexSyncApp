using Log4NetLibrary;
using Microsoft.AspNetCore.Hosting;
using ShopifySharp;
using ShopifySharp.Filters;
using SyncAppEntities.Models;
using SyncAppEntities.Models.EF;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SyncAppCommon.Helpers;
using SyncAppCommon;

namespace SyncAppEntities.Logic
{
    public class ExportDailySalesLogic
    {
        private static readonly log4net.ILog _log = Logger.GetLogger();
        private readonly ShopifyAppContext _context;
        private readonly IWebHostEnvironment _hostingEnvironment;

        private static readonly object salesFileLock = new object();

        public ExportDailySalesLogic(ShopifyAppContext context, IWebHostEnvironment hostingEnvironment)
        {
            _context = context;
            _hostingEnvironment = hostingEnvironment;
        }

        private Configrations Config
        {
            get
            {
                return _context.Configrations.First();
            }
        }
        private WarehouseLogic warehouseLogic
        {
            get
            {
                return new WarehouseLogic(_context);
            }
        }

        private string DefaultWarehouseCode
        {
            get
            {
                var defaultWarehouse = new WarehouseLogic(_context).GetDefaultWarehouseCode();
                if (string.IsNullOrEmpty(defaultWarehouse))
                    defaultWarehouse = Config.WareHouseCode;

                return defaultWarehouse;
            }
        }

        private string NoWarehouseCode
        {
            get
            {
                return $"NOWHCODE";
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
        private string InvoiceFileName
        {
            get
            {
                return "invoices-web-" + DateTime.Now.ToString("yyMMdd") + ".dat";
            }
        }
        private string ShortBranchCodeSales
        {
            get
            {
                return Config.BranchcodeSalesInvoices ?? string.Empty;
            }
        }
        private string CustomerCode
        {
            get
            {
                return Config.CustoemrCode ?? string.Empty;
            }
        }
        private string CustomerCodeWithLeadingSpaces
        {
            get
            {
                return CustomerCode.InsertLeadingSpaces(16);
            }
        }
        private string WareHouseCode
        {
            get
            {
                return Config.WareHouseCode ?? string.Empty;
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
        private int TaxPercentage
        {
            get
            {
                return Config.TaxPercentage.GetValueOrDefault();
            }
        }
        private string SuperPharmCustomerCode
        {
            get
            {
                return Config.SuperPharmCustomerCode ?? string.Empty;
            }
        }
        private string SuperPharmSalesBranchCode
        {
            get
            {
                return Config.SuperPharmSalesBranchCode ?? string.Empty;
            }
        }
        private string SuperPharmCustomerCodeWithLeadingSpaces
        {
            get
            {
                return SuperPharmCustomerCode.InsertLeadingSpaces(16);
            }
        }
        #endregion

        public async Task<List<Order>> ExportDailySalesAsync(DateTime dateToRetriveFrom, DateTime dateToRetriveTo)
        {
            _log.Info($"Start ExportDailySalesAsync - Start Date:" + dateToRetriveFrom + "- EndDate:" + dateToRetriveTo);
            List<Order> lsOfOrders = new List<Order>();
            RefundedOrders refunded = new RefundedOrders();
            try
            {
                lsOfOrders = await new GetShopifyOrders(StoreUrl, ApiSecret, _context).GetNotExportedOrdersAsync(dateToRetriveFrom, dateToRetriveTo);
            }
            catch (ShopifyException e) when (e.Message.ToLower().Contains("exceeded 2 calls per second for api client") || (int)e.HttpStatusCode == 429 /* Too many requests */)
            {
                await Task.Delay(10000);

                lsOfOrders = await new GetShopifyOrders(StoreUrl, ApiSecret, _context).GetNotExportedOrdersAsync(dateToRetriveFrom, dateToRetriveTo);
            }

            try
            {
                await Task.Delay(1000);
                refunded = await new GetShopifyOrders(StoreUrl, ApiSecret, _context).GetRefundedOrdersAsync(dateToRetriveFrom, dateToRetriveTo, TaxPercentage);
            }
            catch (ShopifyException e) when (e.Message.ToLower().Contains("exceeded 2 calls per second for api client") || (int)e.HttpStatusCode == 429 /* Too many requests */)
            {
                await Task.Delay(10000);

                refunded = await new GetShopifyOrders(StoreUrl, ApiSecret, _context).GetRefundedOrdersAsync(dateToRetriveFrom, dateToRetriveTo, TaxPercentage);
            }

            if (refunded?.Orders?.Count > 0)
            {
                lsOfOrders.AddRange(refunded?.Orders);
            }

            lsOfOrders = lsOfOrders.OrderByDescending(a => a.CreatedAt.GetValueOrDefault().DateTime).ToList();
            return lsOfOrders;
        }

        public string GenerateSalesFile(List<Order> orders, bool fromWeb, Dictionary<string, List<string>> lsOfTagTobeAdded = null)
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
                    decimal taxPercentage = (decimal)Config.TaxPercentage;
                    var InvoiceDate = DayOrders.OrdersDate;

                    var BookNum = ShortBranchCodeSales + InvoiceDate.ToString("ddMMyy");
                    var superPharmBookNum = SuperPharmSalesBranchCode + InvoiceDate.ToString("ddMMyy");

                    lock (salesFileLock)
                    {
                        file.WriteLine(
                           "0" +
                           "\t" + CustomerCodeWithLeadingSpaces +
                           "\t" + InvoiceDate.ToString("dd/MM/y") + // order . creation , closed , processing date , invloice date must reagrding to payment please confirm.
                           "\t" + BookNum +
                           "\t" + "".InsertLeadingSpaces(4) + "\t" + WareHouseCode +
                           "\t" + ShortBranchCodeSales
                           );

                    }

                    var regularOrders = DayOrders.Data.Where(o => !o.Tags.ToLower().Contains("super-pharm")).ToList();
                    foreach (var order in regularOrders)
                    {
                        WriteOrderTransactions(file, taxPercentage, order);
                    }


                    var superPharmOrders = DayOrders.Data.Where(o => o.Tags.ToLower().Contains("super-pharm")).ToList();
                    if (superPharmOrders.Count > 0)
                    {
                        //Super Pharm Line 0
                        lock (salesFileLock)
                        {
                            file.WriteLine(
                              "0" +
                              "\t" + SuperPharmCustomerCodeWithLeadingSpaces +
                              "\t" + InvoiceDate.ToString("dd/MM/y") + // order . creation , closed , processing date , invloice date must reagrding to payment please confirm.
                              "\t" + superPharmBookNum +
                              "\t" + "".InsertLeadingSpaces(4) + "\t" + WareHouseCode +
                              "\t" + SuperPharmSalesBranchCode
                              );
                        }

                        foreach (var order in superPharmOrders)
                        {
                            WriteOrderTransactions(file, taxPercentage, order, true);
                        }
                    }
                }

                file.Close();
            }

            var FtpSuccesfully = true; // if web always true

            if (!fromWeb)
            {
                FtpSuccesfully = FtpHandler.UploadFile(FileName, System.IO.File.ReadAllBytes(path), Host, FTPPathConsts.IN_PATH, UserName, Password);
                string subject = "Generate Sales File Status";
                var body = EmailMessages.messageBody("Generate Sales File", "Success", "Invoices and Receipts/" + FileName);
                Utility.SendEmail(SmtpHost, SmtpPort, EmailUserName, EmailPassword, DisplayName, ToEmail, body, subject);
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
                var body = EmailMessages.messageBody("Generate Sales File", "Failed", "Invoices and Receipts/" + FileName);
                Utility.SendEmail(SmtpHost, SmtpPort, EmailUserName, EmailPassword, DisplayName, ToEmail, body, subject);
            }

            return FileName;
        }

        private void WriteOrderTransactions(StreamWriter file, decimal taxPercentage, Order order, bool isSuperPharmOrder = false)
        {
            _log.Info($"Start WriteOrderTransactions");
            var discountZero = 0;
            var shipRefOrder = order;

            string warehouseCode = DefaultWarehouseCode;
            _log.Info($"DefaultWarehouseCode" + warehouseCode);
            //FOR TESTING INVENTORY STUFF
            var ProductServices = new ProductService(StoreUrl, ApiSecret);
            var InventoryLevelsServices = new InventoryLevelService(StoreUrl, ApiSecret);

            foreach (var orderItem in order.LineItems)
            {
                //Product was refunded to another warehouse
                if (orderItem.LocationId.HasValue)
                {
                    warehouseCode = GetWarehouseCodeByLocationId(orderItem.LocationId);
                    _log.Info($"orderItem:" + orderItem.SKU + "-LocationId:" + orderItem.LocationId + "-DefaultWarehouseCode" + warehouseCode);
                }
                else
                {
                    //product is still in the same warehouse
                    if (orderItem.ProductId.HasValue)
                    {
                        var ProductObj = ProductServices.GetAsync(orderItem.ProductId.Value).Result;
                        var VariantObj = ProductObj.Variants.FirstOrDefault(a => (a.Id == orderItem.VariantId) || (a.SKU == orderItem.SKU));

                        if (VariantObj != null && !string.IsNullOrEmpty(VariantObj.SKU))
                        {
                            orderItem.SKU = VariantObj.SKU;
                        }

                        var InventoryItemIds = new List<long>() { VariantObj.InventoryItemId.GetValueOrDefault() };
                        var InventoryItemId = new List<long>() { VariantObj.InventoryItemId.GetValueOrDefault() }.FirstOrDefault();  
                        
                        var LocationQuery = InventoryLevelsServices.ListAsync(new InventoryLevelListFilter { InventoryItemIds = InventoryItemIds }).Result;
                        _log.Info($"orderItem:" + orderItem.SKU + "-LocationQuery.Items.Count():" + LocationQuery.Items.Count());

                        if (orderItem.FulfillmentStatus == "fulfilled" && order.RefundKind == "no_refund"
                            && LocationQuery.Items.Count() > 1)
                        {
                            warehouseCode = "ON01";
                            _log.Info($"Updated warehouseCode" + warehouseCode);
                        }
                        else
                        {
                            var LocationId = LocationQuery.Items.FirstOrDefault().LocationId;
                            warehouseCode = GetWarehouseCodeByLocationId(LocationId);
                            _log.Info($"warehouseCode" + warehouseCode + "LocationId" + LocationId);
                        }
                    }
                }

                if (orderItem.GiftCard.GetValueOrDefault() || (orderItem.FulfillmentService == "gift_card" && orderItem.FulfillmentStatus == "fulfilled"))
                    continue;

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

                if((order.RefundKind != "no_refund" || order.IsRefundOrder) && !order.Transactions.Any())
                {
                    price = 0;
                }

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
                    order.CreatedAt.GetValueOrDefault().ToString("dd/MM/y HH:mm")
                    + "\t" +
                    warehouseCode);
                }

                if (order.FulfillmentStatus == null && order.FinancialStatus == "paid" && order.RefundKind == "no_refund" && order.Refunds.Any())
                {
                    decimal restockPrice = 0;

                    lock (salesFileLock)
                    {
                        file.WriteLine(
                        "1" + "\t" +
                        orderItem.SKU.InsertLeadingSpaces(15) + "\t" + // part number , need confirmation because max lenght is 15
                        "-1".InsertLeadingSpaces(10) + "\t" + // total quantity 
                        restockPrice.GetNumberWithDecimalPlaces(4).InsertLeadingSpaces(10) + "\t" + // unit price without tax
                        "".InsertLeadingSpaces(4) + "\t" + // agent code
                        discountZero.ToString("F") +
                        "\t" + "\t" + "\t" +
                        order.OrderNumber.GetValueOrDefault().ToString().InsertLeadingSpaces(24)
                        + "\t" +
                        order.CreatedAt.GetValueOrDefault().ToString("dd/MM/y HH:mm")
                        + "\t" +
                        warehouseCode);
                    }
                }
            }

            var shipOrder = order;

            var shippingAmount = (shipOrder.ShippingLines?.Sum(a => a.Price).GetValueOrDefault()).ValueWithoutTax(taxPercentage);

            //If the order (e.g partially/refunded or paid) 
            //has shipping cost and this cost is not refunded,
            //then write shipping data
            if (shippingAmount > 0 && (shipOrder.FinancialStatus == "refunded" || shipOrder.RefundKind != "refund_discrepancy"))
            {
                var mQuant = "1";
                if (shipOrder.RefundKind == "shipping_refund" || (shipOrder.FinancialStatus == "refunded" && shipOrder.RefundKind != "no_refund"))
                {
                    mQuant = "-1";
                }

                string partNumber = "921";
                if (isSuperPharmOrder)
                {
                    partNumber = "922";
                }

                lock (salesFileLock)
                {
                    file.WriteLine(
                    "1" + "\t" +
                    partNumber.InsertLeadingSpaces(15) + "\t" +
                    mQuant.ToString().InsertLeadingSpaces(10).InsertLeadingSpaces(10) + "\t" + // total quantity 
                    shippingAmount.GetNumberWithDecimalPlaces(4).InsertLeadingSpaces(10) + "\t" + // unit price without tax
                    "".InsertLeadingSpaces(4) + "\t" + // agent code
                    discountZero.ToString("F") +
                    "\t" + "\t" + "\t" +
                    order.OrderNumber.GetValueOrDefault().ToString().InsertLeadingSpaces(24)
                    + "\t" +
                    order.CreatedAt.GetValueOrDefault().ToString("dd/MM/y HH:mm")
                    + "\t" +
                    warehouseCode);
                }
            }

            if (order.LineItems.Count() == 0 && shipOrder.RefundKind != "shipping_refund")
            {
                var mQuant = "-1";

                var refundedAmount = Math.Abs(order.RefundAmount.ValueWithoutTax(taxPercentage));

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
                    order.CreatedAt.GetValueOrDefault().ToString("dd/MM/y HH:mm")
                    + "\t" +
                    warehouseCode);
                }
            }
        }

        private string GetWarehouseCodeByLocationId(long? locationId)
        {
            _log.Info($"Start GetWarehouseCodeByLocationId");
            string warehouseCode = DefaultWarehouseCode;

            if (locationId != null && locationId != 0)
            {
                warehouseCode = warehouseLogic.GetWarehouse(locationId.Value)?.WarehouseCode;

                if (string.IsNullOrWhiteSpace(warehouseCode))
                {
                    warehouseCode = NoWarehouseCode;
                }
            }
            _log.Info($"End GetWarehouseCodeByLocationId");
            return warehouseCode;
        }
    }
}
