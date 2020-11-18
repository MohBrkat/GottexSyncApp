using Log4NetLibrary;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;
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
    public class ExportDailyReceiptsLogic
    {
        private static readonly log4net.ILog _log = Logger.GetLogger();
        private readonly ShopifyAppContext _context;
        private readonly IHostingEnvironment _hostingEnvironment;

        private static readonly object reciptsFileLock = new object();

        public ExportDailyReceiptsLogic(ShopifyAppContext context, IHostingEnvironment hostingEnvironment)
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
        private string ReceiptsFileName
        {
            get
            {
                return "Receipts-Web-" + DateTime.Now.ToString("yyMMdd") + ".dat";
            }
        }
        private string CustomerCode
        {
            get
            {
                return _config.CustoemrCode;
            }
        }
        private string ShortBranchCodeRecipt
        {
            get
            {
                return _config.BranchCodeReceipt;
            }
        }
        private string _shortBranchCodeReciptsWithLeadingspaces
        {
            get
            {
                return ShortBranchCodeRecipt.ToString().InsertLeadingSpaces(8);
            }
        }
        private string _customerCodeWithLeadingSpaces
        {
            get
            {
                return CustomerCode.InsertLeadingSpaces(16);
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

        public async Task<List<Order>> ExportDailyReceiptsAsync(DateTime dateToRetriveFrom, DateTime dateToRetriveTo)
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
        public async Task<string> GenerateReceiptFileAsync(List<Order> orders, bool fromWeb, Dictionary<string, List<string>> lsOfTagTobeAdded = null)
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
                var body = EmailMessages.messageBody("Generate Receipt File", "Success", "Invoices and Receipts/" + FileName);
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
                var body = EmailMessages.messageBody("Generate Receipt File", "Failed", "Invoices and Receipts/" + FileName);
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
    }
}