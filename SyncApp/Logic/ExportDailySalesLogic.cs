﻿using Log4NetLibrary;
using Microsoft.AspNetCore.Hosting;
using ShopifyApp2;
using ShopifySharp;
using SyncApp.Helpers;
using SyncApp.Models;
using SyncApp.Models.EF;
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
        private string InvoiceFileName
        {
            get
            {
                return "Sales-Web-" + ShortBranchCodeSales + "-" + DateTime.Now.ToString("yyMMdd") + ".dat";
            }
        }
        private string ShortBranchCodeSales
        {
            get
            {
                return _config.BranchcodeSalesInvoices;
            }
        }
        private string CustomerCode
        {
            get
            {
                return _config.CustoemrCode;
            }
        }
        private string _customerCodeWithLeadingSpaces
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
                return _config.WareHouseCode;
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
        private int taxPercentage
        {
            get
            {
                return _config.TaxPercentage ?? 0;
            }
        }
        #endregion

        public async Task<List<Order>> ExportDailySalesAsync(DateTime dateToRetriveFrom, DateTime dateToRetriveTo)
        {
            List<Order> lsOfOrders = new List<Order>();
            RefundedOrders refunded = new RefundedOrders();
            try
            {
                lsOfOrders = await new GetShopifyOrders(StoreUrl, api_secret).GetNotExportedOrdersAsync(dateToRetriveFrom, dateToRetriveTo);
            }
            catch (ShopifyException e) when (e.Message.ToLower().Contains("exceeded 2 calls per second for api client") || (int)e.HttpStatusCode == 429 /* Too many requests */)
            {
                await Task.Delay(10000);

                lsOfOrders = await new GetShopifyOrders(StoreUrl, api_secret).GetNotExportedOrdersAsync(dateToRetriveFrom, dateToRetriveTo);
            }

            try
            {
                await Task.Delay(1000);
                refunded = await new GetShopifyOrders(StoreUrl, api_secret).GetRefundedOrdersAsync(dateToRetriveFrom, dateToRetriveTo, taxPercentage);
            }
            catch (ShopifyException e) when (e.Message.ToLower().Contains("exceeded 2 calls per second for api client") || (int)e.HttpStatusCode == 429 /* Too many requests */)
            {
                await Task.Delay(10000);

                refunded = await new GetShopifyOrders(StoreUrl, api_secret).GetRefundedOrdersAsync(dateToRetriveFrom, dateToRetriveTo, taxPercentage);
            }

            if (refunded?.Orders?.Count > 0)
            {
                lsOfOrders.AddRange(refunded?.Orders);
            }

            lsOfOrders = lsOfOrders.OrderByDescending(a => a.CreatedAt.GetValueOrDefault().DateTime).ToList();
            return lsOfOrders;
        }

        public string GenerateSalesFile(List<Order> orders, bool fromWeb)
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
                    decimal vatTax = taxPercentage + 0.0m;

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
                                order.CreatedAt.GetValueOrDefault().ToString("dd/MM/y HH:mm")
                                + "\t" +
                                vatTax);
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
                                order.CreatedAt.GetValueOrDefault().ToString("dd/MM/y HH:mm")
                                 + "\t" +
                                vatTax);
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
                var body = EmailMessages.messageBody("Generate Sales File", "Success", "Invoices and Receipts/" + FileName);
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
                var body = EmailMessages.messageBody("Generate Sales File", "Failed", "Invoices and Receipts/" + FileName);
                Utility.SendEmail(smtpHost, smtpPort, emailUserName, emailPassword, displayName, toEmail, body, subject);
            }

            return FileName;
        }
    }
}