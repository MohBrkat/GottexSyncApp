using System;
using System.Collections.Generic;

namespace SyncAppEntities.Models.EF
{
    public partial class Configrations
    {
        public int Id { get; set; }
        public string FtpHost { get; set; }
        public string FtpUserName { get; set; }
        public string FtpPassword { get; set; }
        public int? FtpPort { get; set; }
        public string StoreUrl { get; set; }
        public string ApiKey { get; set; }
        public string ApiSecret { get; set; }
        public int? InventoryUpdateHour { get; set; }
        public int? InventoryUpdateMinute { get; set; }
        public string SmtpHost { get; set; }
        public int? SmtpPort { get; set; }
        public string SenderEmail { get; set; }
        public string SenderemailPassword { get; set; }
        public string DisplayName { get; set; }
        public string NotificationEmail { get; set; }
        public string WareHouseCode { get; set; }
        public string CustoemrCode { get; set; }
        public string BranchCodeReceipt { get; set; }
        public string BranchcodeSalesInvoices { get; set; }
        public int? DailySalesHour { get; set; }
        public int? DailySalesMinute { get; set; }
        public int? DailyRecieptsHour { get; set; }
        public int? DailyRecieptsMinute { get; set; }
        public bool? UseRecurringJob { get; set; }
        public int? InventoryUpdateEveryMinute { get; set; }
        public int? TaxPercentage { get; set; }
        public int? DailyReportHour { get; set; }
        public int? DailyReportMinute { get; set; }
        public string ReportEmailAddress1 { get; set; }
        public string ReportEmailAddress2 { get; set; }
        public bool? Saturday { get; set; }
        public bool? Sunday { get; set; }
        public bool? Monday { get; set; }
        public bool? Tuesday { get; set; }
        public bool? Wednesday { get; set; }
        public bool? Thursday { get; set; }
        public bool? Friday { get; set; }
        public string SiteName { get; set; }
        public bool? ExcludeShippingFeesInSales { get; set; }
        public string PayPlusUrl { get; set; }
        public string PayPlusApiKey { get; set; }
        public string PayPlusSecretKey { get; set; }
        public string SuperPharmCustomerCode { get; set; }
        public string SuperPharmReceiptBranchCode { get; set; }
        public string SuperPharmSalesBranchCode { get; set; }
        public int? SuperPharmPaymentCode { get; set; }
        public int? RefundOrdersHistoryDays { get; set; }
    }
}
