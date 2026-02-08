using Newtonsoft.Json;
using ShopifySharp;
using ShopifySharp.Entities;
using SyncAppEntities.ViewModel;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace SyncAppCommon.Helpers
{
    public class ManualTransactionsHelper
    {
        public List<ManualTransaction> GetManualTransactions(IEnumerable<MetaField> metaFields, string key, [Optional] int? orderNumber)
        {
            var manualTransactions = metaFields.FirstOrDefault(mf => mf.Key == key);

            if (manualTransactions?.Value == null) return null;

            var manualTransactionsList = JsonConvert.DeserializeObject<List<ManualTransaction>>(manualTransactions.Value.ToString());

            return manualTransactionsList;
        }

        public void AddManualTransaction(List<Receipt> receiptTransactions, List<ManualTransaction> manualTransactions)
        {
            if (manualTransactions == null || manualTransactions?.Count == 0) return;

            var receipts = manualTransactions.Select(mt => new Receipt
            {
                payment_id = mt.PayplusTransactionReference,
                x_timestamp = mt.CreatedAt.ToString(),
                amount = mt.Amount.ToString(),
                isManualTransaction = true,
            });

            receiptTransactions.AddRange(receipts);
        }
    }
}
