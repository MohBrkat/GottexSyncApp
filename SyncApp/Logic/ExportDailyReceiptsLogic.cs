using Log4NetLibrary;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;
using SyncAppEntities.Models;
using SyncAppEntities.Models.EF;
using SyncAppEntities.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ShopifySharp;
using SyncAppCommon.Helpers;
using SyncAppCommon;
using ShopifySharp.Entities;

namespace SyncAppEntities.Logic
{
    public class ExportDailyReceiptsLogic
    {
        private static readonly log4net.ILog _log = Logger.GetLogger();
        private readonly ShopifyAppContext _context;
        private readonly IWebHostEnvironment _hostingEnvironment;

        private static readonly object receiptsFileLock = new object();

        public ExportDailyReceiptsLogic(ShopifyAppContext context, IWebHostEnvironment hostingEnvironment)
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

        private PayPlusLogic _payPlusLogic
        {
            get
            {
                return new PayPlusLogic(_context);
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
        private static string ReceiptsFileName
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
        private string ShortBranchCodeReceipt
        {
            get
            {
                return Config.BranchCodeReceipt ?? string.Empty;
            }
        }
        private string ShortBranchCodeReceiptsWithLeadingSpaces
        {
            get
            {
                return ShortBranchCodeReceipt.ToString().InsertLeadingSpaces(8);
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
        private string SuperPharmCustomerCode
        {
            get
            {
                return Config.SuperPharmCustomerCode ?? string.Empty;
            }
        }
        private string SuperPharmReceiptBranchCode
        {
            get
            {
                return Config.SuperPharmReceiptBranchCode ?? string.Empty;
            }
        }
        private int SuperPharmPaymentCode
        {
            get
            {
                return Config.SuperPharmPaymentCode.GetValueOrDefault();
            }
        }
        private string SuperPharmCustomerCodeWithLeadingSpaces
        {
            get
            {
                return SuperPharmCustomerCode.InsertLeadingSpaces(16);
            }
        }
        private string SuperPharmReceiptBranchCodeWithLeadingSpaces
        {
            get
            {
                return SuperPharmReceiptBranchCode.ToString().InsertLeadingSpaces(8);
            }
        }
        #endregion

        public async Task<List<Order>> ExportDailyReceiptsAsync(DateTime dateToRetriveFrom, DateTime dateToRetriveTo)
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

            if (dateToRetriveFrom == default)
            {
                dateToRetriveFrom = DateTime.Now.AddDays(-1).Date; // by default
            }
            if (dateToRetriveTo == default)
            {
                dateToRetriveTo = DateTime.Now.AddDays(-1).Date;
            }

            dateToRetriveFrom = dateToRetriveFrom.Date;
            dateToRetriveTo = dateToRetriveTo.Date;

            var lsOfFilteredOrders = lsOfOrders.Where(a => a.CreatedAt.GetValueOrDefault().Date >= dateToRetriveFrom && a.CreatedAt.GetValueOrDefault().Date <= dateToRetriveTo).ToList();

            lsOfFilteredOrders = lsOfFilteredOrders.OrderByDescending(a => a.CreatedAt.GetValueOrDefault().DateTime).ToList();
            return lsOfFilteredOrders;
        }

        public async Task<string> GenerateReceiptFileAsync(List<Order> orders, bool fromWeb, DateTime dateToRetriveFrom, DateTime dateToRetriveTo, Dictionary<string, List<string>> lsOfTagTobeAdded = null)
        {
            var FileName = ReceiptsFileName.Clone().ToString();
            var FolderDirectory = "/Data/receipts/";
            string path = _hostingEnvironment.WebRootPath + "/" + FolderDirectory + "/" + FileName;

            var ordersGroupedByDate = orders
                   .GroupBy(o => o.CreatedAt.GetValueOrDefault().Date)
                   .Select(g => new { OrdersDate = g.Key, Data = g.ToList() });

            using (FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(fileStream))
            {
                foreach (var DayOrders in ordersGroupedByDate)
                {
                    var regularOrders = DayOrders.Data.Where(o => !o.Tags.ToLower().Contains("super-pharm")).ToList();
                    foreach (var order in regularOrders)
                    {
                        await WriteReceiptTransactions(orders, dateToRetriveFrom, dateToRetriveTo, file, order, false);
                    }


                    var superPharmOrders = DayOrders.Data.Where(o => o.Tags.ToLower().Contains("super-pharm")).ToList();
                    foreach (var order in superPharmOrders)
                    {
                        await WriteReceiptTransactions(orders, dateToRetriveFrom, dateToRetriveTo, file, order, true);
                    }
                }

                file.Close();
            }


            var FtpSuccesfully = true;

            if (!fromWeb)
            {

                FtpSuccesfully = FtpHandler.UploadFile(FileName, System.IO.File.ReadAllBytes(path), Host, FTPPathConsts.IN_PATH, UserName, Password);
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

        private async Task WriteReceiptTransactions(List<Order> orders, DateTime dateToRetriveFrom, DateTime dateToRetriveTo, StreamWriter file, Order order, bool isSuperPharmOrder)
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
                transactionsModel = await GetTransactionModelByOrderAsync(order, dateToRetriveFrom, dateToRetriveTo);
            }
            catch (ShopifyException e) when (e.Message.ToLower().Contains("exceeded 2 calls per second for api client") || (int)e.HttpStatusCode == 429 /* Too many requests */)
            {
                await Task.Delay(10000);

                transactionsModel = await GetTransactionModelByOrderAsync(order, dateToRetriveFrom, dateToRetriveTo);
            }

            var InvoiceNumber = GetInvoiceNumber(order);
            var priceWithTaxes = order.TotalPrice;

            var invoiceDate = order.CreatedAt.GetValueOrDefault().ToString("dd/MM/yy");

            var giftCardItems = order.LineItems.Where(li => li.GiftCard == true).ToList();

            if (giftCardItems != null)
            {
                foreach (var giftCardItem in giftCardItems)
                {
                    var giftCardQuantity = giftCardItem.Quantity != null && giftCardItem.Quantity != 0 ? giftCardItem.Quantity : 1;
                    priceWithTaxes -= (giftCardItem.Price * giftCardQuantity);
                }
            }

            //if it's a refund make it [minus] and [priceWithoutTaxes = priceWithTaxes]
            if (order.RefundKind != "no_refund")
            {
                priceWithTaxes *= -1;
            }

            lock (receiptsFileLock)
            {
                if (transactionsModel != null)
                {
                    if (transactionsModel.ReceiptTransactions != null)
                    {
                        if (transactionsModel.ReceiptTransactions.Count() > 0 ||
                           (transactionsModel.GiftCardTransactions.Count() != 0 && transactionsModel.ReceiptTransactions.Count() == 0))
                        {
                            if (!isSuperPharmOrder)
                            {
                                file.WriteLine(
                                "0" +  // spaces to fit indexes
                                " " + CustomerCodeWithLeadingSpaces +
                                " " + invoiceDate + // order . creation , closed , processing date , invloice date must reagrding to payment please confirm.
                                " " + InvoiceNumber.InsertLeadingSpaces(13) + "".InsertLeadingSpaces(5) + // per indexes
                                " " + ShortBranchCodeReceiptsWithLeadingSpaces + "".InsertLeadingSpaces(18) +
                                " " + priceWithTaxes.GetNumberWithDecimalPlaces(2).InsertLeadingSpaces(13));
                            }
                            else
                            {
                                file.WriteLine(
                                "0" +  // spaces to fit indexes
                                " " + SuperPharmCustomerCodeWithLeadingSpaces +
                                " " + invoiceDate + // order . creation , closed , processing date , invloice date must reagrding to payment please confirm.
                                " " + InvoiceNumber.InsertLeadingSpaces(13) + "".InsertLeadingSpaces(5) + // per indexes
                                " " + SuperPharmReceiptBranchCodeWithLeadingSpaces + "".InsertLeadingSpaces(18) +
                                " " + priceWithTaxes.GetNumberWithDecimalPlaces(2).InsertLeadingSpaces(13));
                            }
                        }

                        foreach (var transaction in transactionsModel.ReceiptTransactions)
                        {
                            decimal amount = order.TotalPrice ?? 0m;
                            decimal? payPlusReceiptAmount = 0m;

                            int paymentMeanCode = 0;
                            if (transaction.payment_id.IsNotNullOrEmpty() || transaction.more_info.IsNotNullOrEmpty())
                            {
                                try
                                {
                                    if (!isSuperPharmOrder)
                                    {
                                        var paymentInfo = _payPlusLogic.GetPaymentInfo(transaction.payment_id, transaction.more_info);
                                        if (paymentInfo != null && paymentInfo.data != null)
                                        {
                                            paymentMeanCode = GetPaymentMeanCode(paymentInfo.data.clearing_name);
                                            payPlusReceiptAmount = (decimal)paymentInfo.data.amount;
                                        }
                                    }
                                    else
                                    {
                                        paymentMeanCode = SuperPharmPaymentCode;
                                    }

                                    if (amount == 0 || transactionsModel.ReceiptTransactions.Count > 1)
                                    {
                                        amount = GetAmountFromTransaction(dateToRetriveFrom, dateToRetriveTo, order, transaction, amount);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _log.Error($"[receipts] : Error while getting IPN or Transaction for order:" + order.OrderNumber, ex);
                                }
                            }

                            if (amount == 0 || (transactionsModel.GiftCardTransactions != null && transactionsModel.GiftCardTransactions.Count > 0))
                            {
                                amount = payPlusReceiptAmount ?? 0m;
                            }

                            if (transaction.isStoreCredit)
                            {
                                if (transaction.amount != null && decimal.TryParse(transaction.amount, out decimal storeCreditAmount))
                                {
                                    amount = storeCreditAmount;
                                    if (amount >= 0)
                                    {
                                        paymentMeanCode = GetPaymentMeanCode("Store Credit");
                                    }
                                    else
                                    {
                                        paymentMeanCode = GetPaymentMeanCode("Receipt Store Credit");
                                    }
                                }
                            }
                            else if (order.RefundKind != "no_refund")
                            {
                                amount *= -1;
                            }

                            if (isSuperPharmOrder)
                            {
                                paymentMeanCode = SuperPharmPaymentCode;
                            }

                            if (transaction.x_timestamp.IsNotNullOrEmpty())
                            {
                                invoiceDate = Convert.ToDateTime(transaction.x_timestamp).ToString("dd/MM/yy");
                            }

                            if (order.Restock == true && !transaction.isStoreCredit &&
                                transactionsModel.ReceiptTransactions.Count == 1 &&
                                transactionsModel.GiftCardTransactions?.Count == 0)
                                amount = (order.TotalPrice ?? 0m) * -1;

                            file.WriteLine(
                            "2" +
                            " " + paymentMeanCode.ToString().InsertLeadingZeros(2) +
                            " " + amount.GetNumberWithDecimalPlaces(2).InsertLeadingSpaces(13) + // total payment amount Or Transaction.Amount
                            " " + "00" + //term code
                            " " + amount.GetNumberWithDecimalPlaces(2).InsertLeadingSpaces(13) + // first payment amount Or Transaction.Amount
                            " " + invoiceDate +
                            " " + "".InsertLeadingSpaces(8) +//card number
                            " " + "".InsertLeadingZeros(16));//Payment account

                            if (giftCardItems != null)
                            {
                                foreach (var giftCardItem in giftCardItems)
                                {
                                    var giftCardQuantity = giftCardItem.Quantity != null && giftCardItem.Quantity != 0 ? giftCardItem.Quantity : 1;
                                    var giftCardAmount = (giftCardItem.Price * giftCardQuantity) * -1;

                                    int giftCardPaymentMeanCode = GetPaymentMeanCode("ReceiptGiftCard");

                                    if (order.Restock == true)
                                        giftCardAmount = (order.TotalPrice ?? 0m) * -1;

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
                        }
                    }

                    if (transactionsModel.GiftCardTransactions != null)
                    {
                        int paymentMeanCode = GetPaymentMeanCode("GiftCard");
                        foreach (var giftCardTransaction in transactionsModel.GiftCardTransactions)
                        {
                            invoiceDate = Convert.ToDateTime(giftCardTransaction.CreatedAt).ToString("dd/MM/yy");

                            var giftCardBalance = giftCardTransaction.Amount;
                            if (order.RefundKind != "no_refund")
                            {
                                giftCardBalance *= -1;
                            }

                            if (order.Restock == true && transactionsModel.ReceiptTransactions?.Count == 0)
                                giftCardBalance = (order.TotalPrice ?? 0m) * -1;

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
        }

        private decimal GetAmountFromTransaction(DateTime dateToRetriveFrom, DateTime dateToRetriveTo, Order order, Receipt transaction, decimal amount)
        {
            var fromDate = dateToRetriveFrom.Date.AbsoluteStart();
            var toDate = dateToRetriveTo.Date.AbsoluteEnd();

            var transactionInfo = _payPlusLogic.GetTransactionDetails(transaction.payment_id, transaction.more_info);
            if (transactionInfo != null && transactionInfo.data != null && transactionInfo.data.Count > 0)
            {
                if (order.RefundKind != "no_refund")
                {
                    var payplusRefundTransaction = transactionInfo.data.FirstOrDefault(t => t.transaction?.transaction_type?.ToLower() == "refund" &&
                                                    Convert.ToDateTime(t.transaction?.date).Date >= fromDate.Date && Convert.ToDateTime(t.transaction?.date).Date <= toDate.Date && (string.IsNullOrEmpty(transaction.transaction_uid) || t.transaction?.transaction_uid == transaction.transaction_uid))?.transaction;

                    if (payplusRefundTransaction != null)
                    {
                        amount = payplusRefundTransaction?.amount ?? 0m;
                    }
                    else
                    {
                        var refundTransaction = order.Transactions?.FirstOrDefault(t => t.Gateway != "gift_card" && t.Kind.ToLower() == "refund" && t.Status.ToLower() == "success"
                                                && t.CreatedAt.GetValueOrDefault().Date >= fromDate.Date && t.CreatedAt.GetValueOrDefault().Date <= toDate.Date);

                        amount = refundTransaction?.Amount ?? 0m;
                    }
                }
                else
                {
                    var payPluschargeTransaction = transactionInfo.data.FirstOrDefault(t => t.transaction?.transaction_type?.ToLower() != "refund" &&
                                                Convert.ToDateTime(t.transaction?.date).Date >= dateToRetriveFrom && Convert.ToDateTime(t.transaction?.date).Date <= dateToRetriveTo && (string.IsNullOrEmpty(transaction.transaction_uid) || t.transaction?.transaction_uid == transaction.transaction_uid))?.transaction;
                    if (payPluschargeTransaction != null)
                    {
                        amount = payPluschargeTransaction?.amount ?? 0m;
                    }
                    else
                    {
                        var chargeTransaction = order.Transactions?.FirstOrDefault(t => t.Gateway != "gift_card" && t.Kind.ToLower() != "refund" && t.Status.ToLower() == "success"
                                                && t.CreatedAt.GetValueOrDefault().Date >= dateToRetriveFrom && t.CreatedAt.GetValueOrDefault().Date <= dateToRetriveTo);

                        amount = chargeTransaction?.Amount ?? 0m;
                    }
                }
            }

            return amount;
        }

        private async Task<TransactionsModel> GetTransactionModelByOrderAsync(Order order, DateTime dateToRetriveFrom, DateTime dateToRetriveTo)
        {            
            TransactionsModel transactionsModel = new TransactionsModel()
            {
                ReceiptTransactions = new List<Receipt>(),
                GiftCardTransactions = new List<GiftCardModel>()
            };

            List<Transaction> giftCardTransactions = new List<Transaction>();
            var receiptTransactions = new List<Transaction>();

            var fromDate = dateToRetriveFrom.Date.AddDays(-2).AbsoluteStart();
            var toDate = dateToRetriveTo.Date.AbsoluteEnd();

            var service = new TransactionService(StoreUrl, ApiSecret);
            var serviceTransactions = await service.ListAsync((long)order.Id);

            var transactions = order.Transactions?.Count() > 0 ? order.Transactions : serviceTransactions;

            var originalTransaction = serviceTransactions.FirstOrDefault(t => t.Gateway != "gift_card" && t.Kind.ToLower() != "refund" && t.Status.ToLower() == "success");

            if (order.RefundKind == "no_refund")
            {
                giftCardTransactions = transactions.Where(t => t.Gateway == "gift_card" && t.Kind.ToLower() != "refund"
                        && t.CreatedAt.GetValueOrDefault().Date >= fromDate && t.CreatedAt.GetValueOrDefault().Date <= toDate).ToList();
                receiptTransactions = transactions.Where(t => t.Gateway != "gift_card" && t.Kind.ToLower() != "refund" && t.Status.ToLower() == "success"
                                            && t.CreatedAt.GetValueOrDefault().Date >= fromDate.Date && t.CreatedAt.GetValueOrDefault().Date <= toDate.Date).ToList();
            }
            else
            {
                giftCardTransactions = transactions.Where(t => t.Gateway == "gift_card" && t.Kind.ToLower() == "refund"
                        && t.CreatedAt.GetValueOrDefault().Date >= fromDate && t.CreatedAt.GetValueOrDefault().Date <= toDate).ToList();
                receiptTransactions = transactions.Where(t => t.Gateway != "gift_card" && t.Kind.ToLower() == "refund" && t.Status.ToLower() == "success"
                                            && t.CreatedAt.GetValueOrDefault().Date >= fromDate.Date && t.CreatedAt.GetValueOrDefault().Date <= toDate.Date).ToList();
            }

            var metaFieldService = new MetaFieldService(StoreUrl, ApiSecret);
            var orderMetaFields = metaFieldService.ListAsync(Convert.ToInt64(order.Id), "orders").Result;
            var storeCreditRefunds = orderMetaFields.Items.FirstOrDefault(mf => mf.Key.Equals("store_credit_refunds"));

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
                            if (storeCreditRefund.Id > 0 && storeCreditRefund.CreditAmount > 0)
                            {
                                transactionsModel.ReceiptTransactions.Add(new Receipt()
                                {
                                    isStoreCredit = true,
                                    amount = (-storeCreditRefund.CreditAmount).ToString()
                                }); 
                            }
                            if (storeCreditRefund.Id > 0 &&
                                !string.IsNullOrWhiteSpace(storeCreditRefund.CreditCompensationAmount) &&
                                decimal.TryParse(storeCreditRefund.CreditCompensationAmount, out decimal compVal) &&
                                compVal > 0)
                            {
                                transactionsModel.ReceiptTransactions.Add(new Receipt()
                                {
                                    isStoreCredit = true,
                                    amount = (-compVal).ToString()
                                });
                            }
                            if (storeCreditRefund.ShippingCreditAmount > 0)
                            {
                                transactionsModel.ReceiptTransactions.Add(new Receipt()
                                {
                                    isStoreCredit = true,
                                    amount = (-storeCreditRefund.ShippingCreditAmount).ToString()
                                });
                            }
                        }
                    }
                    else
                    {
                        var storeCreditShipping = storeCreditValue.Refunds.FirstOrDefault(x => x.ShippingCreditAmount > 0 && x.Id == 0)?.ShippingCreditAmount ?? 0;
                        if (storeCreditShipping > 0)
                        {
                            transactionsModel.ReceiptTransactions.Add(new Receipt()
                            {
                                isStoreCredit = true,
                                amount = (-storeCreditShipping).ToString()
                            }); 
                        }
                    }
                }
            }

            foreach (var giftCardTransaction in giftCardTransactions)
            {
                transactionsModel.GiftCardTransactions.Add(new GiftCardModel
                {
                    Receipt = JsonConvert.DeserializeObject<GiftCardReceipt>(giftCardTransaction.Receipt.ToString()),
                    Amount = giftCardTransaction.Amount.GetValueOrDefault(),
                    Status = giftCardTransaction.Status,
                    CreatedAt = giftCardTransaction.CreatedAt.ToString(),
                    Currency = giftCardTransaction.Currency,
                    Kind = giftCardTransaction.Kind,
                    TransactionId = giftCardTransaction.Id.GetValueOrDefault(),
                    Gateway = giftCardTransaction.Gateway
                });
            }
            var totalReceiptTransactions = transactionsModel?.ReceiptTransactions?
                .Select(r => decimal.TryParse(r.amount, out var value) ? Math.Abs(value) : 0m)
                .Sum() ?? 0m;

            if (receiptTransactions?.Any() == true && order.TotalPrice > totalReceiptTransactions)
            {
                foreach (var receiptTransaction in receiptTransactions)
                {
                    var receipt = JsonConvert.DeserializeObject<Receipt>(receiptTransaction.Receipt.ToString());
                    receipt.isStoreCredit = receiptTransaction.Gateway == "shopify_store_credit";
                    receipt.x_timestamp = receiptTransaction.CreatedAt.ToString();
                    if (receipt.isStoreCredit)
                    {
                        var amountText = receiptTransaction.Amount?.ToString();

                        if (!string.IsNullOrEmpty(amountText) &&
                            string.Equals(receiptTransaction.Kind, "refund", StringComparison.OrdinalIgnoreCase))
                        {
                            amountText = "-" + amountText;
                        }

                        receipt.amount = amountText;
                    }
                    else
                    {
                        var originalReceipt = JsonConvert.DeserializeObject<Receipt>(originalTransaction?.Receipt?.ToString());
                        receipt.payment_id = receipt.payment_id.IsNotNullOrEmpty() ? receipt.payment_id : originalReceipt?.payment_id;
                        receipt.more_info = receipt.more_info.IsNotNullOrEmpty() ? receipt.more_info : originalReceipt?.more_info;
                    }
                    transactionsModel.ReceiptTransactions.Add(receipt); 
                }
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

            var paymentMean = _context.PaymentMeans.Where(a => a.Name.ToLower() == company.ToLower()).FirstOrDefault();
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