using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncAppEntities.Models.PayPlus
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    public class Results
    {
        public string status { get; set; }
        public int code { get; set; }
        public string description { get; set; }
    }

    public class Data
    {
        public string transaction_uid { get; set; }
        public string page_request_uid { get; set; }
        public string type { get; set; }
        public string method { get; set; }
        public string number { get; set; }
        public string date { get; set; }
        public string status { get; set; }
        public string status_code { get; set; }
        public string status_description { get; set; }
        public double amount { get; set; }
        public string currency { get; set; }
        public string credit_terms { get; set; }
        public int number_of_payments { get; set; }
        public bool? secure3D_status { get; set; }
        public bool? secure3D_tracking { get; set; }
        public string approval_num { get; set; }
        public string card_foreign { get; set; }
        public string voucher_num { get; set; }
        public string more_info { get; set; }
        public object add_data { get; set; }
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
        public bool? alternative_method { get; set; }
        public string customer_name { get; set; }
        public string customer_name_invoice { get; set; }
        public string identification_number { get; set; }
        public int clearing_id { get; set; }
        public int brand_id { get; set; }
        public string issuer_id { get; set; }
        public object extra_3 { get; set; }
        public string card_holder_name { get; set; }
        public string card_bin { get; set; }
        public string clearing_name { get; set; }
        public string brand_name { get; set; }
        public string issuer_name { get; set; }
        public RelatedTransactions related_transactions { get; set; }
    }

    public class RelatedTransactions
    {
        public List<Child> childs { get; set; }
    }

    public class IPNItem
    {
        public string product_uid { get; set; }
        public string name { get; set; }
        public int price { get; set; }
        public int quantity { get; set; }
        public object discount_value { get; set; }
        public object discount_type { get; set; }
        public object product_invoice_extra_details { get; set; }
        public string barcode { get; set; }
    }

    public class Child
    {
        public string transaction_uid { get; set; }
        public object page_request_uid { get; set; }
        public bool is_multiple_transaction { get; set; }
        public string type { get; set; }
        public string method { get; set; }
        public string number { get; set; }
        public string date { get; set; }
        public string status { get; set; }
        public string status_code { get; set; }
        public string status_description { get; set; }
        public int amount { get; set; }
        public string currency { get; set; }
        public string credit_terms { get; set; }
        public int number_of_payments { get; set; }
        public bool secure3D_status { get; set; }
        public bool secure3D_tracking { get; set; }
        public string approval_num { get; set; }
        public string card_foreign { get; set; }
        public string voucher_num { get; set; }
        public string more_info { get; set; }
        public object add_data { get; set; }
        public string customer_uid { get; set; }
        public string customer_email { get; set; }
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
        public bool alternative_method { get; set; }
        public string customer_name { get; set; }
        public string customer_name_invoice { get; set; }
        public string identification_number { get; set; }
        public int clearing_id { get; set; }
        public int brand_id { get; set; }
        public string issuer_id { get; set; }
        public object extra_3 { get; set; }
        public object card_holder_name { get; set; }
        public string card_bin { get; set; }
        public List<IPNItem> items { get; set; }
        public string clearing_name { get; set; }
        public string brand_name { get; set; }
        public string issuer_name { get; set; }
    }

    public class PayPlusIPNResponse
    {
        public Results results { get; set; }
        public Data data { get; set; }
    }
}
