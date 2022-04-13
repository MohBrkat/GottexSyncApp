﻿using Log4NetLibrary;
using Microsoft.AspNetCore.Hosting;
using ShopifyApp2;
using ShopifySharp;
using SyncApp.Helpers;
using SyncApp.Models;
using SyncApp.Models.EF;
using SyncApp.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SyncApp.Logic
{
    public class ExportDailySalesLogic
    {
        private static readonly log4net.ILog _log = Logger.GetLogger();
        private readonly ShopifyAppContext _context;
        private readonly IHostingEnvironment _hostingEnvironment;

        private static readonly object salesFileLock = new object();

        public ExportDailySalesLogic(ShopifyAppContext context, IHostingEnvironment hostingEnvironment)
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

        private CountriesLogic countriesLogic
        {
            get
            {
                return new CountriesLogic(_context);
            }
        }

        private Countries DefaultCountry
        {
            get
            {
                var defaultCountry = countriesLogic.GetDefaultCountry();
                return defaultCountry;
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
                return "Sales-Web-{branchCode}-" + DateTime.Now.ToString("yyMMdd") + ".dat";
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
        private bool ExcludeShippingFeesInSales
        {
            get
            {
                return Config.ExcludeShippingFeesInSales.GetValueOrDefault();
            }
        }
        #endregion

        public async Task<List<CountryOrders>> ExportDailySalesAsync(DateTime dateToRetriveFrom, DateTime dateToRetriveTo)
        {
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

            var countryOrders = lsOfOrders.OrderByDescending(a => a.CreatedAt.GetValueOrDefault().DateTime).GroupBy(o => o.ShippingAddress.Country)
                        .Select(g => new CountryOrders { Country = g.Key, Orders = g.ToList() }).ToList();

            //lsOfOrders = lsOfOrders.OrderByDescending(a => a.CreatedAt.GetValueOrDefault().DateTime).ToList();
            return countryOrders;
        }

        public string GenerateSalesFile(string country, List<Order> orders, bool fromWeb)
        {
            //Get Country By Name from DB
            var countryDB = GetCountryByName(country);

            //Get Country needed information
            string customerCode = GetCustomerCode(countryDB);
            string branchCode = GetBranchCode(countryDB);

            //Get FileName and Replace with Coutnry Branch Code
            var FileName = InvoiceFileName.Clone().ToString();
            FileName = FileName.Replace("{branchCode}", branchCode);

            var FolderDirectory = "/Data/invoices/";
            var path = _hostingEnvironment.WebRootPath + "/" + FolderDirectory + FileName;

            var ordersGroupedByDate = orders.GroupBy(o => o.CreatedAt.GetValueOrDefault().Date).Select(g => new { OrdersDate = g.Key, Data = g.ToList() });

            using (FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(fileStream))
            {
                foreach (var DayOrders in ordersGroupedByDate)
                {
                    decimal taxPercentage = countryDB?.CountryTax * 100 ?? TaxPercentage;
                    var InvoiceDate = DayOrders.OrdersDate;
                    decimal vatTax = taxPercentage + 0.0m;

                    var BookNum = branchCode + InvoiceDate.ToString("ddMMyy");

                    lock (salesFileLock)
                    {
                        file.WriteLine(
                       "0" +
                       "\t" + customerCode +
                       "\t" + InvoiceDate.ToString("dd/MM/y") + // order . creation , closed , processing date , invloice date must reagrding to payment please confirm.
                       "\t" + BookNum +
                       "\t" + "".InsertLeadingSpaces(4) + "\t" + WareHouseCode +
                       "\t" + branchCode
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
                                order.CreatedAt.GetValueOrDefault().ToString("dd/MM/y HH:mm")
                                + "\t" +
                                vatTax);
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
                                    discountPercentage.ToString("F") +
                                    "\t" + "\t" + "\t" +
                                    order.OrderNumber.GetValueOrDefault().ToString().InsertLeadingSpaces(24)
                                    + "\t" +
                                    order.CreatedAt.GetValueOrDefault().ToString("dd/MM/y HH:mm")
                                    + "\t" +
                                    vatTax);
                                }
                            }
                        }


                        var discountZero = 0;
                        var shipOrder = order;

                        var shippingAmount = (shipOrder.ShippingLines?.Sum(a => a.Price).GetValueOrDefault()).ValueWithoutTax(taxPercentage);

                        //If the order (e.g partially/refunded or paid) 
                        //has shipping cost and this cost is not refunded,
                        //then write shipping data
                        if (shippingAmount > 0 && !ExcludeShippingFeesInSales && (shipOrder.FinancialStatus == "refunded" || shipOrder.RefundKind != "refund_discrepancy"))
                        {
                            var mQuant = "1";
                            if (shipOrder.RefundKind == "shipping_refund" || (shipOrder.FinancialStatus == "refunded" && shipOrder.RefundKind != "no_refund"))
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
                                order.CreatedAt.GetValueOrDefault().ToString("dd/MM/y HH:mm")
                                 + "\t" +
                                vatTax);
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
                FtpSuccesfully = FtpHandler.UploadFile(FileName, System.IO.File.ReadAllBytes(path), Host, FTPPathConsts.IN_PATH, UserName, Password);
                string subject = $"Generate Sales File Status for {country}";
                var body = EmailMessages.messageBody($"Generate Sales File for Country: {country}", "Success", "Invoices and Receipts/" + FileName);
                Utility.SendEmail(SmtpHost, SmtpPort, EmailUserName, EmailPassword, DisplayName, ToEmail, body, subject);
            }

            if (FtpSuccesfully)
            {
                if (!fromWeb)
                {
                    _log.Info($"{FileName} [sales] for country {country} Uploaded sucesfully - the time is : {DateTime.Now}");
                }
            }
            else
            {
                _log.Error($"[sales] : Error during upload {FileName} for country {country} to ftp");
                string subject = $"Generate Sales File Status for {country}";
                var body = EmailMessages.messageBody($"Generate Sales File for Country: {country}", "Failed", "Invoices and Receipts/" + FileName);
                Utility.SendEmail(SmtpHost, SmtpPort, EmailUserName, EmailPassword, DisplayName, ToEmail, body, subject);
            }

            return FileName;
        }

        private string GetBranchCode(Countries countryDB)
        {
            if (string.IsNullOrEmpty(countryDB?.BranchCode))
                return ShortBranchCodeSales;

            return countryDB.BranchCode.Trim();
        }

        private string GetCustomerCode(Countries countryDB)
        {
            if (string.IsNullOrEmpty(countryDB?.CustomerCode))
                return CustomerCodeWithLeadingSpaces;

            return countryDB.CustomerCode.InsertLeadingSpaces(16);
        }

        private Countries GetCountryByName(string countryName)
        {
            var country = DefaultCountry;

            if (!string.IsNullOrEmpty(countryName))
            {
                country = countriesLogic.GetCountryByName(countryName);
            }

            return country;
        }
    }
}