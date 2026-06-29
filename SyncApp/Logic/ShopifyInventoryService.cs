using ShopifySharp;
using SyncAppCommon.Exceptions;
using SyncAppCommon.Helpers;
using SyncAppCommon.Models.GraphQlDTOs;
using System.Linq;
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
        public async Task SetQuantityAsync(long? inventoryItemId, long? locationId, int quantity)
        {
            if (inventoryItemId == null || locationId == null) return;

            var mutationQuery = ShopifyGraphQlHelper.ConstructInventoryUpdateMutation(inventoryItemId.GetValueOrDefault(), locationId.GetValueOrDefault(), quantity, true);

            var result = await graphService.PostAsync(mutationQuery);

            var response = result.ToObject<InventorySetResponse>();

            var errors = response.InventorySetQuantities.UserErrors;

            if (errors?.Count > 0)
            {
                var messages = string.Join(", ", errors.Select(e => e.Message));
                throw new InventoryUpdateException(messages);
            }
        }

        public async Task AdjustQuantityAsync(long? inventoryItemId, long? locationId, int adjustedQuantity)
        {
            if (inventoryItemId == null || locationId == null) return;

            var mutationQuery = ShopifyGraphQlHelper.ConstructInventoryAdjustMutation(inventoryItemId.GetValueOrDefault(), locationId.GetValueOrDefault(), adjustedQuantity);

            var result = await graphService.PostAsync(mutationQuery);

            var response = result.ToObject<InventoryAdjustResponse>();

            var errors = response.InventoryAdjustQuantities.UserErrors;

            if (errors?.Count > 0)
            {
                var messages = string.Join(", ", errors.Select(e => e.Message));
                throw new InventoryUpdateException(messages);
            }
        }
    }
}
