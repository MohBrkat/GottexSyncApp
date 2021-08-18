using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncApp.Models
{
    public static class DateTimeExtensions
    {
        public static DateTimeOffset AbsoluteStart(this DateTime dateTime)
        {
            TimeZoneInfo infotime = TimeZoneInfo.Local;
            var date = DateTimeWithZone(dateTime, infotime);
            return date;
        }

        /// <summary>
        /// Gets the 11:59:59 instance of a DateTime
        /// </summary>
        public static DateTimeOffset AbsoluteEnd(this DateTime dateTime)
        {
            return AbsoluteStart(dateTime).AddDays(1).AddSeconds(-1);
        }

        public static DateTimeOffset DateTimeWithZone(DateTime dateTime, TimeZoneInfo timeZone)
        {
            var convertedDate = new DateTimeOffset(new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second, DateTimeKind.Unspecified), timeZone.BaseUtcOffset);
            return convertedDate;
        }
    }
}
