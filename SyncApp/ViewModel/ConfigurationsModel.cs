using SyncApp.Models.EF;
using SyncApp.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncApp.ViewModel
{
    public class ConfigurationsModel
    {
        public Configrations Configurations { get; set; }
        public ScheduleReports FirstTimeSchedule { get; set; } = new ScheduleReports { TimeOfDay = (int)TimeOfDayEnum.FirstTime };
        public ScheduleReports SecondTimeSchedule { get; set; } = new ScheduleReports { TimeOfDay = (int)TimeOfDayEnum.SecondTime };
        public ScheduleReports ThirdTimeSchedule { get; set; } = new ScheduleReports { TimeOfDay = (int)TimeOfDayEnum.ThirdTime };
    }
}
