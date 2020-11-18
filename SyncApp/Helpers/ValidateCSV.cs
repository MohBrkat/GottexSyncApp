using ShopifyApp2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncApp.Helpers
{
    public static class ValidateCSV
    {
        public static bool IsValidHeaders(string Headers)
        {
            var arr = Headers.Trim().ToLower().Split(",");

            return
                (arr[0].ToLower().Trim()).Contains("Product Handle".Trim().ToLower()) &&
                arr[1].ToLower().Trim().Contains("Variant SKU".Trim().ToLower()) &&
                arr[2].ToLower().Trim().Contains("Method".Trim().ToLower()) &&
                arr[3].ToLower().Trim().Contains("Quantity".Trim().ToLower());
        }
        public static bool IsValidRow(string Row)
        {
            var arr = Row.Trim().ToLower().Split(",");
            return arr[0].IsNotNullOrEmpty() && arr[1].IsNotNullOrEmpty() && arr[2].IsNotNullOrEmpty() && arr[3].IsNotNullOrEmpty();
        }
    }
}
