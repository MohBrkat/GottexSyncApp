using Newtonsoft.Json;

namespace SyncAppEntities.Models.PayPlus
{
    public class PayPlusManualTransactionRequest
    {
        [JsonProperty("voucher_num")]
        public string VoucherNumber { get; set; }

        [JsonProperty("related_transaction")]
        public bool RelatedTransaction { get; set; }
    }
}
