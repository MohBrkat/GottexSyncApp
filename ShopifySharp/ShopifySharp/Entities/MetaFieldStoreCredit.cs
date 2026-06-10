using System.Collections.Generic;
using Newtonsoft.Json;

namespace ShopifySharp.Entities
{
    public class MetaFieldStoreCredit
    {
        [JsonProperty("refunds")]
        public List<StoreCreditRefund> Refunds { get; set; }
    }

    public class StoreCreditRefund
    {
        [JsonProperty("id")]
        public long? Id { get; set; }

        [JsonProperty("credit_amount")]
        public decimal CreditAmount { get; set; }

        [JsonProperty("refund_amount")]
        public decimal RefundAmount { get; set; }

        [JsonProperty("shipping_credit_amount")]
        public decimal ShippingCreditAmount { get; set; }

        [JsonProperty("shipping_amount")]
        public decimal ShippingAmount { get; set; }

        [JsonProperty("credit_compensation_amount")]
        public string CreditCompensationAmount { get; set; }
    }
}
