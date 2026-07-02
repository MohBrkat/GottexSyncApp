using Newtonsoft.Json;
using System.Collections.Generic;
using System;

namespace SyncAppCommon.Models.GraphQlDTOs
{
    public class InventorySetResponse
    {
        [JsonProperty("inventorySetQuantities")]
        public InventorySetQuantitiesPayload InventorySetQuantities { get; set; }
    }

    public class InventorySetQuantitiesPayload
    {
        [JsonProperty("inventoryAdjustmentGroup")]
        public InventoryAdjustmentGroup InventoryAdjustmentGroup { get; set; }

        [JsonProperty("userErrors")]
        public List<UserErrorDto> UserErrors { get; set; } = new List<UserErrorDto>();
    }

    public class InventoryAdjustmentGroup
    {
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("changes")]
        public List<InventoryChangeDto> Changes { get; set; } = new List<InventoryChangeDto>();
    }

    public class InventoryChangeDto
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("delta")]
        public int Delta { get; set; }
    }

    public class UserErrorDto
    {
        [JsonProperty("field")]
        public List<string> Field { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }
}