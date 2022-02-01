﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncApp.Models.PayPlus
{
    public class PayPlusRequest
    {
        public string transaction_uid { get; set; }
        public string payment_request_uid { get; set; }
        public string more_info { get; set; }
        public bool related_transaction { get; set; }
    }
}
