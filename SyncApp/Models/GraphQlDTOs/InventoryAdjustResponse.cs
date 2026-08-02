using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace SyncApp.Models.GraphQlDTOs
{
    public class InventoryAdjustResponse
    {
        [JsonProperty("inventoryAdjustQuantities")]
        public InventoryAdjustQuantitiesPayload InventoryAdjustQuantities { get; set; }
    }

    public class InventoryAdjustQuantitiesPayload
    {
        [JsonProperty("inventoryAdjustmentGroup")]
        public InventoryAdjustmentGroupInfo InventoryAdjustmentGroup { get; set; }

        [JsonProperty("userErrors")]
        public List<AdjustUserErrorDto> UserErrors { get; set; } = new List<AdjustUserErrorDto>();
    }

    public class InventoryAdjustmentGroupInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("changes")]
        public List<InventoryAdjustChangeDto> Changes { get; set; } = new List<InventoryAdjustChangeDto>();
    }

    public class InventoryAdjustChangeDto
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("quantityAfterChange")]
        public int? QuantityAfterChange { get; set; }
    }

    public class AdjustUserErrorDto
    {
        [JsonProperty("field")]
        public List<string> Field { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }
}