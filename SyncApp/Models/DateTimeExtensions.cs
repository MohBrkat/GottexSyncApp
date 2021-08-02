using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncApp.Models
{
    public static class DateTimeExtensions
    {
        public static DateTime AbsoluteStart(this DateTime dateTime)
        {
            TimeZoneInfo infotime = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
            var date = DateTimeWithZone(dateTime.Date, infotime).Date;
            return date;
        }

        /// <summary>
        /// Gets the 11:59:59 instance of a DateTime
        /// </summary>
        public static DateTime AbsoluteEnd(this DateTime dateTime)
        {
            return AbsoluteStart(dateTime).AddDays(1).AddSeconds(-1);
        }

        public static DateTime DateTimeWithZone(DateTime dateTime, TimeZoneInfo timeZone)
        {
            DateTime convertedTime = TimeZoneInfo.ConvertTimeFromUtc(dateTime, timeZone);
            return convertedTime;
        }
    }
}
