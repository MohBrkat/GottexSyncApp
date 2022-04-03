using SyncApp.Models.EF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncApp.ViewModel
{
    public class WarehouseModel
    {
        public List<Warehouses> WarehousesList { get; set; }
        public string Message { get; set; }
    }

    public class Warehouse
    {
        public int Id { get; set; }
        public long? WarehouseId { get; set; }
        public string WarehouseName { get; set; }
        public string WarehouseCode { get; set; }
        public bool IsDefault { get; set; }
    }
}
