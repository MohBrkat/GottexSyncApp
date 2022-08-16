using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncAppEntities.Models
{
    public class SummarizedAutomaticReportModel
    {
        public string ProductVendor { get; set; }
        public string VariantSKU { get; set; }
        public string ProductBarcode { get; set; }
        public int OrderedQuantity { get; set; }
    }
}
