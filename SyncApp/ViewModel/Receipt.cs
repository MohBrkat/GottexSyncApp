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
        public string x_timestamp { get; set; }
        public string x_signature { get; set; }
        public string transaction_uid { get; set; }
        public string page_request_uid { get; set; }
        public string type { get; set; }
        public string method { get; set; }
        public string number { get; set; }
        public string date { get; set; }
        public string status { get; set; }
        public string status_code { get; set; }
        public string status_description { get; set; }
        public string amount { get; set; }
        public string currency { get; set; }
        public string credit_terms { get; set; }
        public string number_of_payments { get; set; }
        public string secure3D_status { get; set; }
        public string secure3D_tracking { get; set; }
        public string approval_num { get; set; }
        public string card_foreign { get; set; }
        public string voucher_num { get; set; }
        public string more_info { get; set; }
        public string add_data { get; set; }
        public string customer_uid { get; set; }
        public string company_name { get; set; }
        public string company_registration_number { get; set; }
        public string terminal_uid { get; set; }
        public string terminal_name { get; set; }
        public string terminal_merchant_number { get; set; }
        public string cashier_uid { get; set; }
        public string cashier_name { get; set; }
        public string four_digits { get; set; }
        public string expiry_month { get; set; }
        public string expiry_year { get; set; }
        public string alternative_method { get; set; }
        public string customer_name { get; set; }
        public string customer_name_invoice { get; set; }
        public string identification_number { get; set; }
        public string clearing_id { get; set; }
        public string brand_id { get; set; }
        public string issuer_id { get; set; }
        public string extra_3 { get; set; }
        public string card_holder_name { get; set; }
        public string card_bin { get; set; }
        public string clearing_name { get; set; }
        public string brand_name { get; set; }
        public string issuer_name { get; set; }
        public string first_payment_amount { get; set; }
        public string rest_payments_amount { get; set; }
        public string token_uid { get; set; }
        public string payment_id { get; set; }
        public string refund_id { get; set; }
    }
}
