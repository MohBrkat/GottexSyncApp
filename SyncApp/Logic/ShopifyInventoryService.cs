using ShopifySharp;
using SyncAppCommon.Helpers;
using SyncAppCommon.Models.GraphQlDTOs;
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

            var mutationQuery = ShopifyGraphQlHelper.ConstructInventoryUpdateMutation(inventoryItemId.GetValueOrDefault(), locationId.GetValueOrDefault(), quantity, true);

            var response = await graphService.PostAsync(mutationQuery);

            return "";
        }

        public async Task<string> AdjustQuantityAsync(long? inventoryItemId, long? locationId, int adjustedQuantity)
        {
            if (inventoryItemId == null || locationId == null) return null;

            var mutationQuery = ShopifyGraphQlHelper.ConstructInventoryAdjustMutation(inventoryItemId.GetValueOrDefault(), locationId.GetValueOrDefault(), adjustedQuantity);

            var response = await graphService.PostAsync(mutationQuery);

            return "";
        }
    }
}
