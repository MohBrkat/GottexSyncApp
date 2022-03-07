using SyncApp.Models.EF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncApp.Logic
{
    public class WarehouseLogic
    {
        private readonly ShopifyAppContext _context;
        public WarehouseLogic(ShopifyAppContext context)
        {
            _context = context;
        }

        public Warehouses GetWarehouse(long warehouseId)
        {
            if (warehouseId != 0)
            {
                var warehouse = _context.Warehouses.FirstOrDefault(w => w.WarehouseId == warehouseId);
                return warehouse;
            }

            return null;
        }

        public string GetDefaultWarehouseCode()
        {
            var warehouse = _context.Warehouses.FirstOrDefault(w => w.IsDefault);
            if (warehouse != null)
                return warehouse.WarehouseCode;

            return string.Empty;
        }

        public long? GetLocationIdByCode(string warehouseCode)
        {
            var warehouse = _context.Warehouses.FirstOrDefault(w => w.WarehouseCode == warehouseCode);
            return warehouse?.WarehouseId;
        }
    }
}
