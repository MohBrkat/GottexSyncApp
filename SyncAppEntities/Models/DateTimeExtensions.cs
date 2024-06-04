using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncAppEntities.Models
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
            // Ensure the DateTime is kind of Unspecified
            var unspecifiedDateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
            var utcOffset = timeZone.GetUtcOffset(unspecifiedDateTime);

            // Use the calculated utcOffset to create DateTimeOffset
            var dateTimeWithZone = new DateTimeOffset(unspecifiedDateTime, utcOffset);
            return dateTimeWithZone;
        }
    }
}
