using ShopifySharp;
using SyncApp.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncApp.Models
{
    public class TransactionsModel
    {
        public List<Receipt> ReceiptTransactions { get; set; }
        public GiftCardModel GiftCardTransaction { get; set; }
        public List<GiftCardModel> GiftCardTransactions { get; set; }
    }
}
