using System;
using System.Collections.Generic;

namespace SyncAppEntities.Models.EF
{
    public partial class PaymentMeans
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int? Code { get; set; }
    }
}
