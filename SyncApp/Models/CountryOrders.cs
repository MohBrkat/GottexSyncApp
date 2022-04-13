using ShopifySharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncApp.Models
{
    public class CountryOrders
    {
        public string Country { get; set; }
        public List<Order> Orders { get; set; }
    }
}
