using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Log4NetLibrary;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ShopifySharp;
using SyncAppEntities.Logic;
using SyncAppEntities.Models.EF;
using SyncAppEntities.ViewModel;

namespace SyncApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SyncAppController : ControllerBase
    {
        private static readonly log4net.ILog _log = Logger.GetLogger();
        private ExportDailyReportsLogic _exportDailyReportsLogic;
        public SyncAppController(ShopifyAppContext context)
        {
            _exportDailyReportsLogic = new ExportDailyReportsLogic(context);
        }

        [Route("GenerateDailyReports")]
        [HttpGet]
        public async Task GenerateDetaildAndSummaryReportsAsync()
        {
            FileModel file = new FileModel();

            try
            {
                List<Order> lsOfOrders = await _exportDailyReportsLogic.ExportDailyReportsAsync(default, default);

                await _exportDailyReportsLogic.GenerateDailyReportFilesAsync(file, lsOfOrders);
            }
            catch (Exception ex)
            {
                _log.Error($"Exception While Exporting The Daily Reports using the job: {JsonConvert.SerializeObject(ex)}");
            }
        }
    }
}
