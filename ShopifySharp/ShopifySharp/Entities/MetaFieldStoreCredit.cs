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
        public long Id { get; set; }

        [JsonProperty("credit_amount")]
        public decimal CreditAmount { get; set; }

        [JsonProperty("shipping_credit_amount")]
        public decimal ShippingCreditAmount { get; set; }
    }
}
