using System.Collections.Generic;
using Newtonsoft.Json;

namespace SyncAppCommon.Models.GraphQlDTOs
{
    public class InventoryItemsResponse
    {
        [JsonProperty("nodes")]
        public List<GraphQlInventoryItemNode> Nodes { get; set; }
    }

    public class InventoryItemsRootResponse
    {
        [JsonProperty("data")]
        public InventoryItemsResponse Data { get; set; }
    }

    public class GraphQlInventoryItemNode
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("inventoryLevels")]
        public GraphQlInventoryLevelsConnection InventoryLevels { get; set; }
    }

    public class GraphQlInventoryLevelsConnection
    {
        [JsonProperty("nodes")]
        public List<GraphQlInventoryLevel> Nodes { get; set; }
    }

    public class GraphQlInventoryLevel
    {
        [JsonProperty("location")]
        public GraphQlLocation Location { get; set; }
    }

    public class GraphQlLocation
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }
}