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
        private string ReceiptsFileName
        {
            get
            {
                return "receipts-web-" + DateTime.Now.ToString("yyMMdd") + ".dat";
            }
        }
        private string CustomerCode
        {
            get
            {
                return Config.CustoemrCode ?? string.Empty;
            }
        }
        private string ShortBranchCodeRecipt
        {
            get
            {
                return Config.BranchCodeReceipt ?? string.Empty;
            }
        }
        private string ShortBranchCodeReciptsWithLeadingspaces
        {
            get
            {
                return ShortBranchCodeRecipt.ToString().InsertLeadingSpaces(8);
            }
        }
        private string CustomerCodeWithLeadingSpaces
        {
            get
            {
                return CustomerCode.InsertLeadingSpaces(16);
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
        #endregion

        public async Task<List<Order>> ExportDailyReceiptsAsync(DateTime dateToRetriveFrom, DateTime dateToRetriveTo)
        {
            List<Order> lsOfOrders = new List<Order>();
            RefundedOrders refunded = new RefundedOrders();
            try
            {
                lsOfOrders = await new GetShopifyOrders(StoreUrl, ApiSecret).GetNotExportedOrdersAsync(dateToRetriveFrom, dateToRetriveTo);
            }
            catch (ShopifyException e) when (e.Message.ToLower().Contains("exceeded 2 calls per second for api client") || (int)e.HttpStatusCode == 429 /* Too many requests */)
            {
                await Task.Delay(10000);

                lsOfOrders = await new GetShopifyOrders(StoreUrl, ApiSecret).GetNotExportedOrdersAsync(dateToRetriveFrom, dateToRetriveTo);
            }

            try
            {
               await Task.Delay(1000);
               refunded = await new GetShopifyOrders(StoreUrl, ApiSecret).GetRefundedOrdersAsync(dateToRetriveFrom, dateToRetriveTo, TaxPercentage);
            }
            catch (ShopifyException e) when (e.Message.ToLower().Contains("exceeded 2 calls per second for api client") || (int)e.HttpStatusCode == 429 /* Too many requests */)
            {
               await Task.Delay(10000);

               refunded = await new GetShopifyOrders(StoreUrl, ApiSecret).GetRefundedOrdersAsync(dateToRetriveFrom, dateToRetriveTo, TaxPercentage);
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
                    //Transactions
                    var index = orders.IndexOf(order);

                    // If this order is not the first, wait for .5 seconds (an average of 2 calls per second).
                    if (index > 0)
                    {
                        await Task.Delay(1000);
                    }

                    TransactionsModel transactionsModel = null;
                    try
                    {
                        transactionsModel = await GetTransactionModelByOrderAsync(order);
                    }
                    catch (ShopifyException e) when (e.Message.ToLower().Contains("exceeded 2 calls per second for api client") || (int)e.HttpStatusCode == 429 /* Too many requests */)
                    {
                        await Task.Delay(10000);

                        transactionsModel = await GetTransactionModelByOrderAsync(order);
                    }

                    var InvoiceNumber = GetInvoiceNumber(order);
                    var priceWithoutTaxes = order.TotalPrice - order.TotalTax;
                    var priceWithTaxes = order.TotalPrice;

                    var invoiceDate = order.CreatedAt.GetValueOrDefault().ToString("dd/MM/yy");

                    var giftCardItem = order.LineItems.FirstOrDefault(li => li.GiftCard == true);

                    if (giftCardItem != null)
                    {
                        priceWithoutTaxes -= giftCardItem.Price;
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
                        " " + CustomerCodeWithLeadingSpaces +
                        " " + invoiceDate + // order . creation , closed , processing date , invloice date must reagrding to payment please confirm.
                        " " + InvoiceNumber.InsertLeadingSpaces(13) + "".InsertLeadingSpaces(5) + // per indexes
                        " " + ShortBranchCodeReciptsWithLeadingspaces + "".InsertLeadingSpaces(18) +
                        " " + priceWithoutTaxes.GetNumberWithDecimalPlaces(2).InsertLeadingSpaces(13));

                        if (transactionsModel != null)
                        {
                            if (transactionsModel.ReceiptTransaction != null)
                            {
                                string company = string.IsNullOrEmpty(transactionsModel.ReceiptTransaction.cc_type) ? transactionsModel.ReceiptTransaction.clearing_name : transactionsModel.ReceiptTransaction.cc_type;
                                int paymentMeanCode = GetPaymentMeanCode(company);
                                if (transactionsModel.ReceiptTransaction.x_timestamp.IsNotNullOrEmpty())
                                {
                                    invoiceDate = Convert.ToDateTime(transactionsModel.ReceiptTransaction.x_timestamp).ToString("dd/MM/yy");
                                }

                                var amount = Convert.ToDecimal(transactionsModel.ReceiptTransaction.x_amount);
                                if (order.RefundKind != "no_refund")
                                {
                                    amount *= -1;
                                }

                                file.WriteLine(
                                "2" +
                                " " + paymentMeanCode.ToString().InsertLeadingZeros(2) +
                                " " + amount.GetNumberWithDecimalPlaces(2).InsertLeadingSpaces(13) + // total payment amount Or Transaction.Amount
                                " " + "00" + //term code
                                " " + amount.GetNumberWithDecimalPlaces(2).InsertLeadingSpaces(13) + // first payment amount Or Transaction.Amount
                                " " + invoiceDate +
                                " " + "".InsertLeadingSpaces(8) +//card number
                                " " + "".InsertLeadingZeros(16));//Payment account

                                if (giftCardItem != null)
                                {
                                    var giftCardAmount = giftCardItem.Price * -1;

                                    int giftCardPaymentMeanCode = GetPaymentMeanCode("ReceiptGiftCard");
                                    file.WriteLine(
                                    "2" +
                                    " " + giftCardPaymentMeanCode.ToString().InsertLeadingZeros(2) +
                                    " " + giftCardAmount.GetNumberWithDecimalPlaces(2).InsertLeadingSpaces(13) + // total payment amount Or Transaction.Amount
                                    " " + "00" + //term code
                                    " " + giftCardAmount.GetNumberWithDecimalPlaces(2).InsertLeadingSpaces(13) + // first payment amount Or Transaction.Amount
                                    " " + invoiceDate +
                                    " " + "".InsertLeadingSpaces(8) +//card number
                                    " " + "".InsertLeadingZeros(16));//Payment account
                                }
                            }

                            if (transactionsModel.GiftCardTransaction != null)
                            {
                                int paymentMeanCode = GetPaymentMeanCode("GiftCard");
                                invoiceDate = Convert.ToDateTime(transactionsModel.GiftCardTransaction.CreatedAt).ToString("dd/MM/yy");

                                var giftCardBalance = transactionsModel.GiftCardTransaction.Amount;
                                if (order.RefundKind != "no_refund")
                                {
                                    giftCardBalance *= -1;
                                }

                                file.WriteLine(
                                "2" +
                                " " + paymentMeanCode.ToString().InsertLeadingZeros(2) +
                                " " + giftCardBalance.GetNumberWithDecimalPlaces(2).InsertLeadingSpaces(13) + // total payment amount Or Transaction.Amount
                                " " + "00" + //term code
                                " " + giftCardBalance.GetNumberWithDecimalPlaces(2).InsertLeadingSpaces(13) + // first payment amount Or Transaction.Amount
                                " " + invoiceDate +
                                " " + "".InsertLeadingSpaces(8) +//card number
                                " " + "".InsertLeadingZeros(16));//Payment account
                            }
                        }
                    }
                }

                file.Close();
            }


            var FtpSuccesfully = true;

            if (!fromWeb)
            {

                FtpSuccesfully = FtpHandler.UploadFile(FileName, System.IO.File.ReadAllBytes(path), Host, "/In", UserName, Password);
                string subject = "Generate Receipt File Status";
                var body = EmailMessages.messageBody("Generate Receipt File", "Success", "Invoices and Receipts/" + FileName);
                Utility.SendEmail(SmtpHost, SmtpPort, EmailUserName, EmailPassword, DisplayName, ToEmail, body, subject);
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
                Utility.SendEmail(SmtpHost, SmtpPort, EmailUserName, EmailPassword, DisplayName, ToEmail, body, subject);
            }

            return FileName;
        }
        private async Task<Receipt> GetTransactionByOrderAsync(Order order)
        {
            Receipt r = null;
            if (order.RefundKind == "no_refund" || !order.Transactions.Any())
            {
                var service = new TransactionService(StoreUrl, ApiSecret);
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
                var transaction = JsonConvert.DeserializeObject<Receipt>(order.Transactions.FirstOrDefault().Receipt.ToString());
                r = JsonConvert.DeserializeObject<Receipt>(
                    order.Transactions.Where(t => t.Kind.ToLower() == "refund").FirstOrDefault().Receipt.ToString());
                r.x_timestamp = order.Transactions.Where(t => t.Kind.ToLower() == "refund").FirstOrDefault().CreatedAt.ToString();
                r.clearing_name = transaction.clearing_name;
            }
            return r;
        }

        private async Task<TransactionsModel> GetTransactionModelByOrderAsync(Order order)
        {
            TransactionsModel transactionsModel = new TransactionsModel();
            if (order.RefundKind == "no_refund" || !order.Transactions.Any())
            {
                var service = new TransactionService(StoreUrl, ApiSecret);
                var transactions = await service.ListAsync((long)order.Id);

                foreach (var transaction in transactions)
                {
                    if (transaction.Gateway == "gift_card")
                    {
                        transactionsModel.GiftCardTransaction = new GiftCardModel
                        {
                            Receipt = JsonConvert.DeserializeObject<GiftCardReceipt>(transaction.Receipt.ToString()),
                            Amount = transaction.Amount.GetValueOrDefault(),
                            Status = transaction.Status,
                            CreatedAt = transaction.CreatedAt.ToString(),
                            Currency = transaction.Currency,
                            Kind = transaction.Kind,
                            TransactionId = transaction.Id.GetValueOrDefault(),
                            Gateway = transaction.Gateway
                        };
                    }
                    else
                    {
                        transactionsModel.ReceiptTransaction = JsonConvert.DeserializeObject<Receipt>(transaction.Receipt.ToString());
                        transactionsModel.ReceiptTransaction.x_timestamp = transaction.CreatedAt.ToString();
                    }
                }
            }
            else
            {
                var transaction = JsonConvert.DeserializeObject<Receipt>(order.Transactions.FirstOrDefault().Receipt.ToString());
                transactionsModel.ReceiptTransaction = JsonConvert.DeserializeObject<Receipt>(
                    order.Transactions.Where(t => t.Kind.ToLower() == "refund").FirstOrDefault().Receipt.ToString());
                transactionsModel.ReceiptTransaction.x_timestamp = order.Transactions.Where(t => t.Kind.ToLower() == "refund").FirstOrDefault().CreatedAt.ToString();
                transactionsModel.ReceiptTransaction.clearing_name = transaction.clearing_name;
            }
            return transactionsModel;
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
