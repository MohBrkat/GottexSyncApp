using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncApp.Models
{
    public class DetailedAutomaticReportModel
    {
        public string OrderName { get; set; }
        public string CustomerName { get; set; }
        public string OrderDay { get; set; }
        public string ProductVendor { get; set; }
        public string VariantSKU { get; set; }
        public string ProductBarcode { get; set; }
        public int OrderedQuantity { get; set; }
        public string CustomerNotes { get; set; }
        public string GlobalEOrderId { get; set; }
    }
}
