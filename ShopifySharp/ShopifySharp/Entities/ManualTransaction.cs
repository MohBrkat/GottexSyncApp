using Newtonsoft.Json;
using System;

namespace ShopifySharp.Entities
{
    public class ManualTransaction
    {
        [JsonProperty("payplus_transaction_reference")]
        public string PayplusTransactionReference { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("amount")]
        public decimal Amount { get; set; }

        [JsonProperty("failed_transaction_id")]
        public long FailedTransactionId { get; set; }
    }
}