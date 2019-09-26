using System;
using System.Collections.Generic;

namespace SyncApp.Models.EF
{
    public partial class PaymentMeans
    {
        public int Id { get; set; }
        public int? Code { get; set; }
        public string Name { get; set; }
    }
}
