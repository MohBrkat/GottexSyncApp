using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncAppEntities.ViewModel
{
    public class GiftCardModel
    {
        public decimal Amount { get; set; }
        public long TransactionId { get; set; }
        public string Currency { get; set; }
        public string Gateway { get; set; }
        public string Status { get; set; }
        public string Kind { get; set; }
        public string CreatedAt { get; set; }
        public GiftCardReceipt Receipt { get; set; }
    }

    public class GiftCardReceipt
    {
        public long gift_card_id { get; set; }
        public string gift_card_last_characters { get; set; }
    }
}
