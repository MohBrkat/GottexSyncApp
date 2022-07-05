using ShopifySharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncAppEntities.Models
{
    public class RefundedOrders
    {
        public Dictionary<string, List<string>> lslsOfTagsToBeAdded { get; set; }
        public List<Order> Orders { get; set; }
    }
}
