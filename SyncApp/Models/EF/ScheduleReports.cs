using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncApp.Models.EF
{
    public class ScheduleReports
    {
        public int id { get; set; }
        public int ReportType { get; set; }
        public int TimeOfDay { get; set; }
        public bool? Saturday { get; set; }
        public bool? Sunday { get; set; }
        public bool? Monday { get; set; }
        public bool? Tuesday { get; set; }
        public bool? Wednesday { get; set; }
        public bool? Thursday { get; set; }
        public bool? Friday { get; set; }
        public int? ScheduleHour { get; set; }
        public int? ScheduleMinutes { get; set; }
    }
}
