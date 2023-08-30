using System;
using System.Collections.Generic;
using System.Linq;
using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShopifyApp2.ViewModel;
using ShopifySharp;
using SyncApp.Models.EF;
using SyncApp.Filters;
using SyncApp.ViewModel;
using System.Threading.Tasks;
using SyncApp.Logic;
using Log4NetLibrary;
using Newtonsoft.Json;
using SyncApp.Models;

namespace ShopifyApp2.Controllers
{
    [Auth]
    public class HomeController : Controller
    {
        private readonly ShopifyAppContext _context;
        private readonly IHostingEnvironment _hostingEnvironment;
        private static readonly log4net.ILog _log = Logger.GetLogger();

        private ImportInventoryFTPLogic _importInventoryFTPLogic;
        private ImportInventoryWebLogic _importInventoryWebLogic;
        private ExportDailySalesLogic _exportDailySalesLogic;
        private ExportDailyReceiptsLogic _exportDailyReceiptsLogic;
        private ExportDailyReportsLogic _exportDailyReportsLogic;
        private CountriesLogic _countriesLogic;

        public HomeController(ShopifyAppContext context, IHostingEnvironment hostingEnvironment)
        {
            _context = context;
            _hostingEnvironment = hostingEnvironment;
            _importInventoryFTPLogic = new ImportInventoryFTPLogic(context);
            _importInventoryWebLogic = new ImportInventoryWebLogic(context);
            _exportDailySalesLogic = new ExportDailySalesLogic(context, hostingEnvironment);
            _exportDailyReceiptsLogic = new ExportDailyReceiptsLogic(context, hostingEnvironment);
            _exportDailyReportsLogic = new ExportDailyReportsLogic(context);
            _countriesLogic = new CountriesLogic(context);
        }

        private Configrations Config
        {
            get
            {
                return _context.Configrations.First();
            }
        }

        private string DefaultCountryName
        {
            get
            {
                var defaultCountryDB = _countriesLogic.GetDefaultCountry()?.CountryName;
                return !string.IsNullOrEmpty(defaultCountryDB) ? defaultCountryDB : "France";
            }
        }

        private string DefaultBranchCode
        {
            get
            {
                var defaultBranchCodeDB = _countriesLogic.GetDefaultCountry()?.BranchCode;
                return !string.IsNullOrEmpty(defaultBranchCodeDB) ? defaultBranchCodeDB : Config.BranchcodeSalesInvoices;
            }
        }

        private string DefaultCustomerCode
        {
            get
            {
                var defaultCustomerCodeDB = _countriesLogic.GetDefaultCountry()?.CustomerCode;
                return !string.IsNullOrEmpty(defaultCustomerCodeDB) ? defaultCustomerCodeDB : Config.CustoemrCode;
            }
        }

        #region prop
        private int InventoryImportEveryMinute
        {
            get
            {
                return Config.InventoryUpdateEveryMinute.GetValueOrDefault();
            }
        }
        private int DailyRecieptsHour
        {
            get
            {
                return Config.DailyRecieptsHour.GetValueOrDefault();
            }
        }
        private int DailyRecieptsMinute
        {
            get
            {
                return Config.DailyRecieptsMinute.GetValueOrDefault();
            }
        }
        private int DailySalesHour
        {
            get
            {
                return Config.DailySalesHour.GetValueOrDefault();
            }
        }
        private int DailySalesMinute
        {
            get
            {
                return Config.DailySalesMinute.GetValueOrDefault();
            }
        }
        private int DailyReportHour
        {
            get
            {
                return Config.DailyReportHour.GetValueOrDefault();
            }
        }
        private int DailyReportMinute
        {
            get
            {
                return Config.DailyReportMinute.GetValueOrDefault();
            }
        }
        #endregion

        public IActionResult Index()
        {
            if (Config.UseRecurringJob.GetValueOrDefault())
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
            RecurringJob.AddOrUpdate(() => DoImoportAsync(), Cron.MinuteInterval(minutes), TimeZoneInfo.Local);
            RecurringJob.AddOrUpdate(() => ExportSalesAsync(false, default, default), SalesCron, TimeZoneInfo.Local);
            //RecurringJob.AddOrUpdate(() => ExportReceiptsAsync(false, default, default), RecieptsCron, TimeZoneInfo.Local);
            RecurringJob.AddOrUpdate(() => ExportReportAsync(false, default, default, string.Empty), ReportsCron, TimeZoneInfo.Local);


            return Ok(new { valid = true });
        }

        #region Import Inventory CSV
        public ActionResult ImportInventoryUpdatesFromCSV()
        {
            return View();
        }

        #region Import from FTP
        [DisableConcurrentExecution(120)]
        public async Task DoImoportAsync()
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
        public async Task<ActionResult> ExportSalesAsync(bool fromWeb, DateTime dateToRetriveFrom = default, DateTime dateToRetriveTo = default)
        {
            List<CountryFiles> files = new List<CountryFiles>();

            try
            {
                GetCountriesFromShopify();
                
                var shopifyCountries = await _countriesLogic.GetCountriesAsync();

                List<CountryOrders> lsOfOrders = await _exportDailySalesLogic.ExportDailySalesAsync(dateToRetriveFrom, dateToRetriveTo);

                var defaultCountryOrders = GetOrCreateDefaultCountryOrders(lsOfOrders, DefaultCountryName);
                var defaultOrders = defaultCountryOrders.Orders.ToList();

                foreach (var CountryOrders in lsOfOrders)
                {
                    var country = CountryOrders.Country;
                    var orders = CountryOrders.Orders;

                    var countryDB = _countriesLogic.GetCountryByName(country);
                    //if it doesn't have values add to the default country orders
                    if ((!_countriesLogic.CheckIfHasValues(countryDB) || IsBranchAndCustomerCodesSame(countryDB)) && !CountryIsDefault(country))
                    {
                        defaultOrders.AddRange(orders);
                        continue;
                    }

                    //add orders for a country with no details saved to the default one
                    if (CountryIsDefault(country))
                        orders.AddRange(defaultOrders);

                    var file = _exportDailySalesLogic.GenerateSalesFile(country, orders, fromWeb);
                    files.Add(new CountryFiles
                    {
                        Country = country,
                        FilePath = file
                    });
                }
                return View("~/Views/Home/ExportDailySales.cshtml", files);
            }
            catch (Exception ex)
            {
                _log.Error($"Exception While Exporting Sales Report: {JsonConvert.SerializeObject(ex)}");
            }

            return View("~/Views/Home/ExportDailySales.cshtml", "N/A");
        }

        private async void GetCountriesFromShopify()
        {
            var shopifyCountries = await _countriesLogic.GetCountriesAsync();
            
            foreach (var country in shopifyCountries)
            {
                var countryDB = _countriesLogic.GetCountry(country.Id ?? 0);
                if (countryDB != null)
                {
                    if (Config.GetTaxFromShopify.GetValueOrDefault())
                    {  
                        countryDB.CountryTax = country.Tax;
                        _context.Update(countryDB);
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    Countries countryModel = new Countries
                    {
                        Id = 0,
                        CountryId = country.Id,
                        CountryName = country.Name,
                        CountryCode = country.Code,                     
                    };
                    if (Config.GetTaxFromShopify.GetValueOrDefault())
                    {
                        countryModel.CountryTax = country.Tax;
                    }
                    _context.Add(countryModel);
                }
            }

            await _context.SaveChangesAsync();

        }

        private CountryOrders GetOrCreateDefaultCountryOrders(List<CountryOrders> orders, string defaultCountryName)
        {
            var defaultCountryOrders = orders.FirstOrDefault(o => CountryIsDefault(o.Country));

            if (defaultCountryOrders == null)
            {
                defaultCountryOrders = new CountryOrders
                {
                    Country = DefaultCountryName.Trim().ToLower(),
                    Orders = new List<Order>()
                };

                orders.Add(defaultCountryOrders);
            }
            else
            {
                orders.Remove(defaultCountryOrders);
                orders.Add(new CountryOrders
                {
                    Country = defaultCountryOrders.Country,
                    Orders = defaultCountryOrders.Orders
                });
            }

            defaultCountryOrders.Orders = new List<Order>();
            return defaultCountryOrders;
        }

        private bool CountryIsDefault(string country)
        {
            return country.Trim().ToLower() == DefaultCountryName.Trim().ToLower();
        }

        private bool IsBranchAndCustomerCodesSame(Countries country)
        {
            return country.BranchCode.Trim().ToLower() == DefaultBranchCode.Trim().ToLower()
                && country.CustomerCode.Trim().ToLower() == DefaultCustomerCode.Trim().ToLower();
        }

        #endregion
        #region Export Daily Receipts

        public ActionResult ExportDailyReceipts()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> ExportReceiptsAsync(bool fromWeb, DateTime dateToRetriveFrom = default, DateTime dateToRetriveTo = default)
        {
            try
            {
                List<Order> lsOfOrders = await _exportDailyReceiptsLogic.ExportDailyReceiptsAsync(dateToRetriveFrom, dateToRetriveTo);
                string path = string.Empty;
                if (lsOfOrders.Count() > 0)
                {
                    path = await _exportDailyReceiptsLogic.GenerateReceiptFileAsync(lsOfOrders, fromWeb);
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
        public async Task<ActionResult> ExportReportAsync(bool fromWeb, DateTime dateToRetriveFrom, DateTime dateToRetriveTo, string reportType = "")
        {
            FileModel file = new FileModel();

            try
            {
                bool isWorkingDay = _exportDailyReportsLogic.CheckWorkingDays();
                if (!isWorkingDay && !fromWeb)
                {
                    return View("~/Views/Home/ExportDailyReport.cshtml");
                }

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
    }
}