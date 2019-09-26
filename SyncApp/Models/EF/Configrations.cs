using System;
using System.Collections.Generic;

namespace SyncApp.Models.EF
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
    }
}
