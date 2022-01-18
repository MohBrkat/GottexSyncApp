using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncApp.Models.PayPlus
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class TransactionResults
    {
        public string status { get; set; }
        public int code { get; set; }
        public string description { get; set; }
    }

    public class Payments
    {
        public int number_of_payments { get; set; }
        public int first_payment_amount { get; set; }
        public int rest_payments_amount { get; set; }
    }

    public class Secure3D
    {
        public bool status { get; set; }
        public object tracking { get; set; }
    }

    public class Transaction
    {
        public string transaction_uid { get; set; }
        public string transaction_type { get; set; }
        public string date { get; set; }
        public string status_code { get; set; }
        public bool transaction_is_cancelled { get; set; }
        public decimal amount { get; set; }
        public string currency { get; set; }
        public string credit_terms { get; set; }
        public Payments payments { get; set; }
        public Secure3D secure3D { get; set; }
        public string approval_number { get; set; }
        public string voucher_number { get; set; }
        public string more_info { get; set; }
    }

    public class Item
    {
        public int discount_amount { get; set; }
        public string discount_type { get; set; }
        public decimal? discount_value { get; set; }
        public int quantity { get; set; }
        public double quantity_price { get; set; }
        public string name { get; set; }
    }

    public class CardInformation
    {
        public string four_digits { get; set; }
        public string expiry_month { get; set; }
        public string expiry_year { get; set; }
    }

    public class TransactionData
    {
        public string customer_uid { get; set; }
        public string terminal_uid { get; set; }
        public string cashier_uid { get; set; }
        public List<Item> items { get; set; }
        public CardInformation card_information { get; set; }
    }

    public class Datum
    {
        public Transaction transaction { get; set; }
        public TransactionData data { get; set; }
    }

    public class PayPlusTransactionResponse
    {
        public TransactionResults results { get; set; }
        public List<Datum> data { get; set; }
    }
}
