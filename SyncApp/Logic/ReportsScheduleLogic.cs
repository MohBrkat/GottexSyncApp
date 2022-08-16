using Microsoft.EntityFrameworkCore;
using SyncAppEntities.Models.EF;
using SyncAppEntities.Models.Enums;
using SyncAppEntities.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncAppEntities.Logic
{
    public class ReportsScheduleLogic
    {
        private ShopifyAppContext _context;
        private int _reportType;

        public ReportsScheduleLogic(ShopifyAppContext context, int reportType)
        {
            this._context = context;
            this._reportType = reportType;
        }

        internal async Task GetScheduleReports(ConfigurationsModel configs)
        {
            configs.FirstTimeSchedule = await GetScheduleReports((int)TimeOfDayEnum.FirstTime, _reportType);
            configs.SecondTimeSchedule = await GetScheduleReports((int)TimeOfDayEnum.SecondTime, _reportType);
            configs.ThirdTimeSchedule = await GetScheduleReports((int)TimeOfDayEnum.ThirdTime, _reportType);
        }


        private async Task<ScheduleReports> GetScheduleReports(int timeOfDay, int reportType)
        {
            return await _context.ScheduleReports.FirstOrDefaultAsync(r => r.TimeOfDay == timeOfDay && r.ReportType == reportType);
        }

        internal async Task UpdateScheduleReportsAsync(ConfigurationsModel configs)
        {
            await AddOrUpdateScheduleReportsAsync(configs.FirstTimeSchedule, _reportType, (int)TimeOfDayEnum.FirstTime);
            await AddOrUpdateScheduleReportsAsync(configs.SecondTimeSchedule, _reportType, (int)TimeOfDayEnum.SecondTime);
            await AddOrUpdateScheduleReportsAsync(configs.ThirdTimeSchedule, _reportType, (int)TimeOfDayEnum.ThirdTime);
        }

        private async Task AddOrUpdateScheduleReportsAsync(ScheduleReports scheduleReports, int reportType, int reportTime)
        {
            var schedule = await _context.ScheduleReports.FirstOrDefaultAsync(r => r.TimeOfDay == reportTime && r.ReportType == reportType);
            if (schedule == null)
            {
                _context.ScheduleReports.Add(scheduleReports);
            }
            else
            {
                UpdateScheduleModel(schedule, scheduleReports);
                _context.ScheduleReports.Update(schedule);
            }
        }

        private void UpdateScheduleModel(ScheduleReports schedule, ScheduleReports reportsScheduler)
        {
            schedule.ScheduleHour = reportsScheduler.ScheduleHour;
            schedule.ScheduleMinutes = reportsScheduler.ScheduleMinutes;
            schedule.Saturday = reportsScheduler.Saturday;
            schedule.Sunday = reportsScheduler.Sunday;
            schedule.Monday = reportsScheduler.Monday;
            schedule.Tuesday = reportsScheduler.Tuesday;
            schedule.Wednesday = reportsScheduler.Wednesday;
            schedule.Thursday = reportsScheduler.Thursday;
            schedule.Friday = reportsScheduler.Friday;
        }
    }
}
