using System;
using System.Collections.Generic;
using System.Linq;
using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShopifyApp2.ViewModel;
using ShopifySharp;
using SyncAppEntities.Models.EF;
using SyncAppEntities.Filters;
using SyncAppEntities.ViewModel;
using System.Threading.Tasks;
using SyncAppEntities.Logic;
using Log4NetLibrary;
using Newtonsoft.Json;
using SyncAppEntities.Models.Enums;

namespace ShopifyApp2.Controllers
{
    [Auth]
    public class HomeController : Controller
    {
        private readonly ShopifyAppContext _context;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private static readonly log4net.ILog _log = Logger.GetLogger();

        private ImportInventoryFTPLogic _importInventoryFTPLogic;
        private ImportInventoryWebLogic _importInventoryWebLogic;
        private ExportDailySalesLogic _exportDailySalesLogic;
        private ExportDailyReceiptsLogic _exportDailyReceiptsLogic;
        private ExportDailyReportsLogic _exportDailyReportsLogic;

        public HomeController(ShopifyAppContext context, IWebHostEnvironment hostingEnvironment)
        {
            _context = context;
            _hostingEnvironment = hostingEnvironment;
            _importInventoryFTPLogic = new ImportInventoryFTPLogic(context);
            _importInventoryWebLogic = new ImportInventoryWebLogic(context);
            _exportDailySalesLogic = new ExportDailySalesLogic(context, hostingEnvironment);
            _exportDailyReceiptsLogic = new ExportDailyReceiptsLogic(context, hostingEnvironment);
            _exportDailyReportsLogic = new ExportDailyReportsLogic(context);
        }

        private Configrations _config
        {
            get
            {
                return _context.Configrations.First();
            }
        }

        #region prop
        private int InventoryImportEveryMinute
        {
            get
            {
                return _config.InventoryUpdateEveryMinute.GetValueOrDefault();
            }
        }
        private int DailyRecieptsHour
        {
            get
            {
                return _config.DailyRecieptsHour.GetValueOrDefault();
            }
        }
        private int DailyRecieptsMinute
        {
            get
            {
                return _config.DailyRecieptsMinute.GetValueOrDefault();
            }
        }
        private int DailySalesHour
        {
            get
            {
                return _config.DailySalesHour.GetValueOrDefault();
            }
        }
        private int DailySalesMinute
        {
            get
            {
                return _config.DailySalesMinute.GetValueOrDefault();
            }
        }
        private int DailyReportHour
        {
            get
            {
                return _config.DailyReportHour.GetValueOrDefault();
            }
        }
        private int DailyReportMinute
        {
            get
            {
                return _config.DailyReportMinute.GetValueOrDefault();
            }
        }
        #endregion

        public IActionResult Index()
        {
            if (_config.UseRecurringJob.GetValueOrDefault())
            {
                IniateProcesses();
            }
            return View();
        }

        [HttpGet]
        public IActionResult IniateProcesses()
        {
            // hangfire
            var SalesCron = Cron.Daily(DailySalesHour, DailySalesMinute);
            var RecieptsCron = Cron.Daily(DailyRecieptsHour, DailyRecieptsMinute);
            var ReportsCron = Cron.Daily(DailyReportHour, DailyReportMinute);

            var minutes = InventoryImportEveryMinute == 0 ? 30 : InventoryImportEveryMinute;
            RecurringJob.AddOrUpdate(() => DoImoport(), Cron.MinuteInterval(minutes), TimeZoneInfo.Local);
            RecurringJob.AddOrUpdate(() => ExportSales(false, default, default), SalesCron, TimeZoneInfo.Local);
            RecurringJob.AddOrUpdate(() => ExportReceipts(false, default, default), RecieptsCron, TimeZoneInfo.Local);

            ScheduleReports((int)ReportTypesEnum.DailyReport);
            //RecurringJob.AddOrUpdate(() => ExportReportAsync(false, default, default, string.Empty), ReportsCron, TimeZoneInfo.Local);

            return Ok(new { valid = true });
        }

        #region Import Inventory CSV

        public ActionResult ImportInventoryUpdatesFromCSV()
        {
            return View();
        }

        #region Import from FTP
        [DisableConcurrentExecution(120)]

        public async Task DoImoport()
        {
            try
            {
                await _importInventoryFTPLogic.ImportInventoryFileAsync();
            }
            catch (Exception ex)
            {
                _log.Error($"Exception While Import From FTP: {JsonConvert.SerializeObject(ex)}");
            }
        }

        #endregion

        #region Import using the website

        [HttpPost]
        public async Task<IActionResult> ImportInventoryUpdatesFromCSV(IFormFile File)
        {
            ImportCSVViewModel model = new ImportCSVViewModel();
            try
            {
                model = await _importInventoryWebLogic.ImportInventoryFileAsync(File);
            }
            catch (Exception ex)
            {
                _log.Error($"Exception While Import From Website: {JsonConvert.SerializeObject(ex)}");
            }

            return View(model);
        }

        #endregion

        #endregion
        #region Export Daily Sales

        public ActionResult ExportDailySales()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> ExportSales(bool fromWeb, DateTime dateToRetriveFrom = default, DateTime dateToRetriveTo = default)
        {
            try
            {
                List<Order> lsOfOrders = await _exportDailySalesLogic.ExportDailySalesAsync(dateToRetriveFrom, dateToRetriveTo);
                if (lsOfOrders.Count() > 0)
                {
                    string path = _exportDailySalesLogic.GenerateSalesFile(lsOfOrders, fromWeb);
                    return View("~/Views/Home/ExportDailySales.cshtml", path);
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Exception While Exporting Sales Report: {JsonConvert.SerializeObject(ex)}");
            }

            return View("~/Views/Home/ExportDailySales.cshtml", "N/A");
        }

        #endregion
        #region Export Daily Receipts

        public ActionResult ExportDailyReceipts()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> ExportReceipts(bool fromWeb, DateTime dateToRetriveFrom = default, DateTime dateToRetriveTo = default)
        {
            try
            {
                List<Order> lsOfOrders = await _exportDailyReceiptsLogic.ExportDailyReceiptsAsync(dateToRetriveFrom, dateToRetriveTo);
                string path = string.Empty;
                if (lsOfOrders.Count() > 0)
                {
                    if (dateToRetriveFrom == default) //Yesterday option (Default)
                    {
                        dateToRetriveFrom = DateTime.Now.AddDays(-1); // by default
                        dateToRetriveTo = DateTime.Now.AddDays(-1);
                    }
                    else if (dateToRetriveTo == default) //Single day option
                    {
                        dateToRetriveTo = dateToRetriveFrom.Date;
                    }

                    path = await _exportDailyReceiptsLogic.GenerateReceiptFileAsync(lsOfOrders, fromWeb, dateToRetriveFrom, dateToRetriveTo);
                    return View("~/Views/Home/ExportDailyReceipts.cshtml", path);
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Exception While Exporting Receipts Report: {JsonConvert.SerializeObject(ex)}");
            }

            return View("~/Views/Home/ExportDailyReceipts.cshtml", "N/A");
        }

        #endregion
        #region Export Daily Report

        public ActionResult ExportDailyReport()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> ExportReport(bool fromWeb, DateTime dateToRetriveFrom, DateTime dateToRetriveTo, string reportType = "")
        {
            FileModel file = new FileModel();

            try
            {
                List<Order> lsOfOrders = await _exportDailyReportsLogic.ExportDailyReportsAsync(dateToRetriveFrom, dateToRetriveTo);

                await _exportDailyReportsLogic.GenerateDailyReportFilesAsync(file, lsOfOrders);
            }
            catch (Exception ex)
            {
                _log.Error($"Exception While Exporting The Daily Reports: {JsonConvert.SerializeObject(ex)}");
            }

            return View("~/Views/Home/ExportDailyReport.cshtml", file);
        }

        #endregion
        #region General
        [HttpPost]
        public FileResult DownloadReport(string fileData, string contentType, string fileName)
        {
            return File(System.Convert.FromBase64String(fileData), contentType, fileName);
        }
        public FileResult DownloadFile(string fileToDownload, string subFolder)
        {
            string filePath = _hostingEnvironment.WebRootPath + $"/{subFolder}/{fileToDownload}";
            string fileName = fileToDownload;

            byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);

            return File(fileBytes, "application/force-download", fileName);
        }

        private void ScheduleReports(int reportType)
        {
            var reportsSchedules = _context.ScheduleReports.Where(r => r.ReportType == reportType).ToList();

            foreach (var report in reportsSchedules)
            {
                ReportHangFire(report.Saturday, DayOfWeek.Saturday, report);
                ReportHangFire(report.Sunday, DayOfWeek.Sunday, report);
                ReportHangFire(report.Monday, DayOfWeek.Monday, report);
                ReportHangFire(report.Tuesday, DayOfWeek.Tuesday, report);
                ReportHangFire(report.Wednesday, DayOfWeek.Wednesday, report);
                ReportHangFire(report.Thursday, DayOfWeek.Thursday, report);
                ReportHangFire(report.Friday, DayOfWeek.Friday, report);
            }
        }

        private void ReportHangFire(bool? dayChecked, DayOfWeek dayOfWeek, ScheduleReports report)
        {
            string recurringId = $"{(ReportTypesEnum)report.ReportType} {(TimeOfDayEnum)report.TimeOfDay} {(int)dayOfWeek}";

            if (dayChecked.HasValue && dayChecked.Value == true)
            {
                if (report.ScheduleHour.HasValue && report.ScheduleMinutes.HasValue)
                {
                    string cron = Cron.Weekly(dayOfWeek, report.ScheduleHour.Value, report.ScheduleMinutes.Value);
                    RecurringJob.AddOrUpdate(recurringId, () => ExportReport(false, default, default, string.Empty), cron, TimeZoneInfo.Local);
                }
            }
            else
            {
                RecurringJob.RemoveIfExists(recurringId);
            }
        }
        #endregion
    }
}