using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncApp.ViewModel
{
    public class Receipt
    {
        public string x_account_id { get; set; }
        public string x_amount { get; set; }
        public string x_currency { get; set; }
        public string x_gateway_reference { get; set; }
        public string x_reference { get; set; }
        public string x_result { get; set; }
        public string x_test { get; set; }
        public string cc_type { get; set; }
        public string cc_number { get; set; }
        public string cc_exp_date { get; set; }
        public string auth_number { get; set; }
        public string num_of_payments { get; set; }
        public string tourist_card { get; set; }
        public string x_timestamp { get; set; }
        public string x_signature { get; set; }

    }
}
