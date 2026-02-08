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
using Newtonsoft.Json;
using ShopifySharp.Entities;

namespace SyncAppEntities.Logic
{
    public class ExportDailySalesLogic
    {
        private static readonly log4net.ILog _log = Logger.GetLogger();
        private readonly ShopifyAppContext _context;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ManualTransactionsHelper _manualTransactionsHelper;

        private static readonly object salesFileLock = new object();

        public ExportDailySalesLogic(ShopifyAppContext context, IWebHostEnvironment hostingEnvironment)
        {
            _context = context;
            _hostingEnvironment = hostingEnvironment;
            _manualTransactionsHelper = new ManualTransactionsHelper();
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

        public async Task<List<Order>> ExportDailySalesAsync(DateTime dateToRetrieveFrom, DateTime dateToRetrieveTo)
        {
            _log.Info($"Start ExportDailySalesAsync - Start Date:" + dateToRetrieveFrom + "- EndDate:" + dateToRetrieveTo);
            List<Order> lsOfOrders = new List<Order>();
            RefundedOrders refunded = new RefundedOrders();
            try
            {
                lsOfOrders = await new GetShopifyOrders(StoreUrl, ApiSecret, _context).GetNotExportedOrdersAsync(dateToRetrieveFrom, dateToRetrieveTo);
            }
            catch (ShopifyException e) when (e.Message.ToLower().Contains("exceeded 2 calls per second for api client") || (int)e.HttpStatusCode == 429 /* Too many requests */)
            {
                await Task.Delay(10000);

                lsOfOrders = await new GetShopifyOrders(StoreUrl, ApiSecret, _context).GetNotExportedOrdersAsync(dateToRetrieveFrom, dateToRetrieveTo);
            }

            try
            {
                await Task.Delay(1000);
                refunded = await new GetShopifyOrders(StoreUrl, ApiSecret, _context).GetRefundedOrdersAsync(dateToRetrieveFrom, dateToRetrieveTo, TaxPercentage);
            }
            catch (ShopifyException e) when (e.Message.ToLower().Contains("exceeded 2 calls per second for api client") || (int)e.HttpStatusCode == 429 /* Too many requests */)
            {
                await Task.Delay(10000);

                refunded = await new GetShopifyOrders(StoreUrl, ApiSecret, _context).GetRefundedOrdersAsync(dateToRetrieveFrom, dateToRetrieveTo, TaxPercentage);
            }

            if (refunded?.Orders?.Count > 0)
            {
                lsOfOrders.AddRange(refunded?.Orders);
            }

            if (dateToRetrieveFrom == default)
            {
                dateToRetrieveFrom = DateTime.Now.AddDays(-1).Date; // by default
            }
            if (dateToRetrieveTo == default)
            {
                dateToRetrieveTo = DateTime.Now.AddDays(-1).Date;
            }

            dateToRetrieveFrom = dateToRetrieveFrom.Date;
            dateToRetrieveTo = dateToRetrieveTo.Date;

            var lsOfFilteredOrders = lsOfOrders.Where(a => a.CreatedAt.GetValueOrDefault().Date >= dateToRetrieveFrom && a.CreatedAt.GetValueOrDefault().Date <= dateToRetrieveTo).ToList();

            lsOfFilteredOrders = lsOfFilteredOrders.OrderByDescending(a => a.CreatedAt.GetValueOrDefault().DateTime).ToList();
            return lsOfFilteredOrders;
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

                        if ((order.RefundKind != "no_refund" || order.IsRefundOrder) && order.Transactions != null && order.Transactions.Any())
                        {
                            Boolean success = false;
                            foreach (Transaction transaction in order.Transactions)
                            {
                                if (transaction.Status == "success")
                                {
                                    success = true;
                                }
                            }
                            if (success)
                            {
                                WriteOrderTransactions(file, taxPercentage, order);
                            }
                        }
                        else
                        {
                            WriteOrderTransactions(file, taxPercentage, order);
                        }
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
                            if ((order.RefundKind != "no_refund" || order.IsRefundOrder) && order.Transactions != null && order.Transactions.Any())
                            {
                                Boolean success = false;
                                foreach (Transaction transaction in order.Transactions)
                                {
                                    if (transaction.Status == "success")
                                    {
                                        success = true;
                                    }
                                }
                                if (success)
                                {
                                    WriteOrderTransactions(file, taxPercentage, order, true);
                                }
                            }
                            else
                            {
                                WriteOrderTransactions(file, taxPercentage, order, true);
                            }
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
            decimal totalWithVatPercentage = ((taxPercentage / 100.0m) + 1.0m);

            string warehouseCode = DefaultWarehouseCode;
            _log.Info($"DefaultWarehouseCode" + warehouseCode);
            //FOR TESTING INVENTORY STUFF
            var ProductServices = new ProductService(StoreUrl, ApiSecret);
            var InventoryLevelsServices = new InventoryLevelService(StoreUrl, ApiSecret);

            // Get Order MetaFields
            var metaFieldService = new MetaFieldService(StoreUrl, ApiSecret);
            var orderMetaFields = metaFieldService.ListAsync(Convert.ToInt64(order.Id), "orders").Result;
            var storeCreditRefunds = orderMetaFields.Items.FirstOrDefault(mf => mf.Key.Equals("store_credit_refunds"));

            var manualTransactions = _manualTransactionsHelper.GetManualTransactions(orderMetaFields.Items, "manual_transactions", order.OrderNumber);

            var createdAtDate = manualTransactions == null ? order.CreatedAt : manualTransactions.FirstOrDefault()?.CreatedAt;

            var storeCreditLineItems = new Dictionary<long, decimal>();
            var isShippingRefund = false;
            decimal? extraStoreCreditVal = null;
            if (storeCreditRefunds?.Value != null)
            {
                var storeCreditValue = JsonConvert.DeserializeObject<MetaFieldStoreCredit>(storeCreditRefunds.Value.ToString());
                if (storeCreditValue.Refunds.Any())
                {
                    if (order.RefundKind == "refund_discrepancy")
                    {
                        var originalRefund = order.Refunds.FirstOrDefault();
                        var storeCreditRefund = storeCreditValue.Refunds.FirstOrDefault(r => r.Id == originalRefund?.Id);
                        if (storeCreditRefund != null)
                        {
                            foreach (var refundLineItem in originalRefund.RefundLineItems)
                            {
                                decimal price;
                                decimal totalDiscount = 0;

                                //Calculate Discount on single lineItem
                                if (refundLineItem.LineItem.DiscountAllocations != null && refundLineItem.LineItem.DiscountAllocations.Count() != 0)
                                {
                                    totalDiscount = refundLineItem.LineItem.DiscountAllocations.Sum(a => decimal.Parse(a.Amount));
                                }

                                decimal toBePerItem = refundLineItem.Quantity < 0 ? 1 : (decimal)refundLineItem.Quantity;
                                //Discounted Price without TAX and Discount
                                price = refundLineItem.LineItem.Price.GetValueOrDefault() - Math.Round(totalDiscount / toBePerItem, 2);

                                if (refundLineItem.LineItem.Taxable == false || order.TaxesIncluded == true)
                                    price /= totalWithVatPercentage;

                                storeCreditLineItems.Add(Convert.ToInt64(refundLineItem.LineItemId), price);
                            }

                            if (storeCreditRefund.ShippingCreditAmount > 0)
                            {

                                isShippingRefund = true;
                            }

                            if (!string.IsNullOrWhiteSpace(storeCreditRefund.CreditCompensationAmount) &&
                                decimal.TryParse(storeCreditRefund.CreditCompensationAmount, out decimal compVal) &&
                                compVal > 0)
                            {
                                extraStoreCreditVal = compVal / totalWithVatPercentage;
                            }
                        }
                    }
                    else
                    {
                        isShippingRefund = storeCreditValue.Refunds.Any(r => r.ShippingCreditAmount > 0 && r.Id == 0);
                    }
                }
            }

            // 1. Flatten all refund line items from all refunds
            var refundLineItems = new List<dynamic>(); // Use a proper type if available
            decimal totalRefundLineSubtotal = 0m;
            decimal totalRefundedAmount = 0m; // sum of all actual refunded transactions (cash)

            if (order.Refunds != null)
            {
                foreach (var refund in order.Refunds)
                {
                    // Sum actual refunded transactions
                    if (refund.Transactions != null)
                    {
                        totalRefundedAmount += refund.Transactions
                            .Where(t => t.Kind == "refund" && t.Status == "success")
                            .Sum(t => t.Amount ?? 0);
            }

                    // Collect item-level subtotals
                    if (refund.RefundLineItems != null)
                    {
                        foreach (var rItem in refund.RefundLineItems)
                        {
                            decimal subtotal = rItem.SubTotal ?? 0;

                            // Convert to net if taxes included
                            if (order.TaxesIncluded == true)
                                subtotal /= totalWithVatPercentage;

                            refundLineItems.Add(new
                            {
                                LineItemId = rItem.LineItemId,
                                SubtotalNet = subtotal
                            });

                            totalRefundLineSubtotal += subtotal;
                        }
                    }
                }
            }

            // 2. Fallback proportional allocation in case some items are missing in refundLineItems
            decimal totalItemsNetAmount = order.LineItems.Sum(li =>
            {
                decimal totalDiscount = 0;
                if (li.DiscountAllocations != null && li.DiscountAllocations.Any())
                    totalDiscount = li.DiscountAllocations.Sum(a => decimal.Parse(a.Amount));

                decimal qty = Math.Abs(li.Quantity ?? 1);
                decimal net = li.Price.GetValueOrDefault() * qty - totalDiscount;

                if (li.Taxable == false || order.TaxesIncluded == true)
                    net /= totalWithVatPercentage;

                return net;
            });

            decimal fallbackRefundRatio = totalItemsNetAmount > 0
                ? Math.Min(totalRefundedAmount / totalItemsNetAmount, 1m)
                : 0m;


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

                        long inventoryItemId = 0;
                        var inventoryItemIds = new List<long>();

                        if (VariantObj?.InventoryItemId != null)
                        {
                            inventoryItemId = VariantObj.InventoryItemId.Value;
                            inventoryItemIds.Add(inventoryItemId);
                        }
                        else
                        {
                            _log.Warn($"VariantObj or InventoryItemId is null for SKU: {orderItem?.SKU}, in order: {order.OrderNumber}");
                        }

                        var locationQuery = inventoryItemIds.Any()
                            ? InventoryLevelsServices
                                .ListAsync(new InventoryLevelListFilter { InventoryItemIds = inventoryItemIds })
                                .Result
                            : null;

                        var locationItems = locationQuery?.Items ?? Enumerable.Empty<InventoryLevel>();
                        _log.Info($"orderItem:{orderItem?.SKU}-LocationQuery.Items.Count():{locationItems.Count()}");

                        if (orderItem?.FulfillmentStatus == "fulfilled"
                            && order?.RefundKind == "no_refund"
                            && locationItems.Count() > 1)
                        {
                            warehouseCode = "ON01";
                            _log.Info($"Updated warehouseCode: {warehouseCode}");
                        }
                        else
                        {
                            var location = locationItems.FirstOrDefault();

                            if (location != null)
                            {
                                var locationId = location.LocationId;
                                warehouseCode = GetWarehouseCodeByLocationId(locationId);
                                _log.Info($"warehouseCode:{warehouseCode} LocationId:{locationId}");
                        }
                        else
                        {
                                _log.Warn($"No inventory locations found for SKU: {orderItem?.SKU}, in order: {order.OrderNumber}");
                            }
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

                decimal toBePerItem = orderItem.Quantity < 0 ? 1 : (decimal)orderItem.Quantity;
                //Discounted Price without TAX and Discount
                price = orderItem.Price.GetValueOrDefault() - Math.Round(totalDiscount / toBePerItem, 2);

                if (orderItem.Taxable == false || order.TaxesIncluded == true)
                    price /= totalWithVatPercentage;

                var defaultPrice = price;

                if (orderItem.Quantity < 0)
                {
                    var refundedItem = refundLineItems.FirstOrDefault(r => r.LineItemId == orderItem.Id);
                    if (refundedItem != null)
                    {
                        // Scale subtotal proportionally to actual refunded cash
                        if (totalRefundLineSubtotal > 0)
                        {
                            decimal proportion = refundedItem.SubtotalNet / totalRefundLineSubtotal;
                            price = proportion * totalRefundedAmount;

                            // Convert to net if taxes included
                            if (order.TaxesIncluded == true)
                                price /= totalWithVatPercentage;

                            // Ensure we never exceed full net price
                            price = Math.Min(price ?? 0, defaultPrice ?? 0);
                        }
                        else
                        {
                            // If somehow subtotal is zero, fallback to full net price
                            price = defaultPrice;
                        }
                    }
                    else
                    {
                        // Item not in refundLineItems → fallback proportional allocation
                        price = defaultPrice * fallbackRefundRatio;
                    }
                }

                if ((order.RefundKind != "no_refund" || order.IsRefundOrder) && !order.Transactions.Any())
                {
                    if (order.RefundKind == "refund_discrepancy" &&
                        storeCreditLineItems.TryGetValue(
                            Convert.ToInt64(orderItem.Id), out var refundPrice))
                    {
                        price = refundPrice;
                    }
                    else
                    {
                        price = 0;
                    }
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
                    createdAtDate.GetValueOrDefault().ToString("dd/MM/y HH:mm")
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
                        createdAtDate.GetValueOrDefault().ToString("dd/MM/y HH:mm")
                        + "\t" +
                        warehouseCode);
                    }
                }
            }

            if (extraStoreCreditVal != null)
            {
                var partNumber = "951";
                var mQuant = "-1";
                lock (salesFileLock)
                {
                    file.WriteLine(
                    "1" + "\t" +
                    partNumber.InsertLeadingSpaces(15) + "\t" +
                    mQuant.ToString().InsertLeadingSpaces(10).InsertLeadingSpaces(10) + "\t" + // total quantity 
                    extraStoreCreditVal.GetNumberWithDecimalPlaces(4).InsertLeadingSpaces(10) + "\t" + // unit price without tax
                    "".InsertLeadingSpaces(4) + "\t" + // agent code
                    discountZero.ToString("F") +
                    "\t" + "\t" + "\t" +
                    order.OrderNumber.GetValueOrDefault().ToString().InsertLeadingSpaces(24)
                    + "\t" +
                    createdAtDate.GetValueOrDefault().ToString("dd/MM/y HH:mm")
                    + "\t" +
                    warehouseCode);
                }
            }

            var shipOrder = order;

            var shippingAmount = (shipOrder.ShippingLines?.Sum(a => a.Price).GetValueOrDefault()).ValueWithoutTax(taxPercentage);

            //If the order (e.g partially/refunded or paid) 
            //has shipping cost and this cost is not refunded,
            //then write shipping data
            if (shippingAmount > 0 && (shipOrder.FinancialStatus == "refunded" || shipOrder.RefundKind != "refund_discrepancy"))
            {
                if (shipOrder.DiscountCodes?.Any(dc => dc.Type == "shipping") == true)
                {
                    var shippingDiscount = shipOrder.DiscountCodes.Where(dc => dc.Type == "shipping").Sum(dc => decimal.Parse(dc.Amount)).ValueWithoutTax(taxPercentage);
                    shippingAmount -= shippingDiscount;
                }

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
                    createdAtDate.GetValueOrDefault().ToString("dd/MM/y HH:mm")
                    + "\t" +
                    warehouseCode);
                }
            }

            if (shippingAmount > 0 && isShippingRefund)
            {
                var mQuant = "-1";

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
                    createdAtDate.GetValueOrDefault().ToString("dd/MM/y HH:mm")
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
                    createdAtDate.GetValueOrDefault().ToString("dd/MM/y HH:mm")
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
