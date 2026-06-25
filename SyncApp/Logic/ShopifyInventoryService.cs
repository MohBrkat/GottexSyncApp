using ShopifySharp;
using SyncAppCommon.Helpers;
using System.Text.Json;
using System.Threading.Tasks;

namespace SyncApp.Logic
{
    public class ShopifyInventoryService
    {
        private readonly GraphService graphService;
        public ShopifyInventoryService(string storeUrl, string apiSecret)
        {
            graphService = new GraphService(storeUrl, apiSecret);
        }
        public async Task<string> SetQuantityAsync(long? inventoryItemId, long? locationId, int quantity)
        {
            if (inventoryItemId == null || locationId == null) return null;

            var mutation = ShopifyGraphQlHelper.ConstructInventoryUpdateMutation();

            var variables = ShopifyGraphQlHelper.GetInventoryUpdateVariables(inventoryItemId.GetValueOrDefault(), locationId.GetValueOrDefault(), quantity);

            var requestBody = new
            {
                query = mutation,
                variables
            };

            string bodyString = JsonSerializer.Serialize(requestBody);

            var response = await graphService.PostAsync(bodyString);

            return "";
        }

        public async Task<string> AdjustQuantityAsync(long? inventoryItemId, long? locationId, int adjustedQuantity)
        {
            if (inventoryItemId == null || locationId == null) return null;

            var mutation = ShopifyGraphQlHelper.ConstructInventoryAdjustMutation();

            var variables = ShopifyGraphQlHelper.GetInventoryAdjustVariables(inventoryItemId.GetValueOrDefault(), locationId.GetValueOrDefault(), adjustedQuantity);

            var requestBody = new
            {
                query = mutation,
                variables
            };

            string bodyString = JsonSerializer.Serialize(requestBody);

            var response = await graphService.PostAsync(bodyString);

            return "";
        }
    }
}
