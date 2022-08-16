using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncAppEntities.Models.EF
{
    public class Warehouses
    {
        public int Id { get; set; }
        public long? WarehouseId { get; set; }
        public string WarehouseName { get; set; }
        public string WarehouseCode { get; set; }
        public bool IsDefault { get; set; }
    }
}
