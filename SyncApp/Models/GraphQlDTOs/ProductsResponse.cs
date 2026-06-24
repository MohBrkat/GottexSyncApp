using System.Collections.Generic;
using Newtonsoft.Json;
using SyncApp.Models.GraphQlDTOs;

namespace SyncAppCommon.Models.GraphQlDTOs
{
    public class ProductsResponse
    {
        [JsonProperty("products")]
        public GraphQlProductsConnection Products { get; set; }
    }

    public class GraphQlProductsConnection
    {
        [JsonProperty("nodes")]
        public List<GraphQlProduct> Nodes { get; set; }

        [JsonProperty("pageInfo")]
        public GraphQlPageInfo PageInfo { get; set; }
    }

    public class GraphQlProduct
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("vendor")]
        public string Vendor { get; set; }

        [JsonProperty("variants")]
        public GraphQlVariantsConnection Variants { get; set; }
    }

    public class GraphQlVariantsConnection
    {
        [JsonProperty("nodes")]
        public List<GraphQlProductVariant> Nodes { get; set; }
    }

    public class GraphQlProductVariant
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("sku")]
        public string Sku { get; set; }

        [JsonProperty("barcode")]
        public string Barcode { get; set; }

        [JsonProperty("inventoryItem")]
        public GraphQlInventoryItem InventoryItem { get; set; }
    }
}