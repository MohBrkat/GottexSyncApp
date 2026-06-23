using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SyncApp.Models.GraphQlDTOs
{
    public class OrdersResponse
    {
        [JsonProperty("orders")]
        public OrdersConnection Orders { get; set; }
    }

    public class OrdersConnection
    {
        [JsonProperty("nodes")]
        public List<GraphQlOrder> Nodes { get; set; }

        [JsonProperty("pageInfo")]
        public GraphQlPageInfo PageInfo { get; set; }
    }

    public class GraphQlOrder
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("createdAt")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonProperty("customer")]
        public GraphQlCustomer Customer { get; set; }

        [JsonProperty("discountApplications")]
        public GraphQlDiscountApplicationsConnection DiscountApplications { get; set; }

        [JsonProperty("displayFinancialStatus")]
        public string DisplayFinancialStatus { get; set; }

        [JsonProperty("displayFulfillmentStatus")]
        public string DisplayFulfillmentStatus { get; set; }

        [JsonProperty("tags")]
        public string[] Tags { get; set; }

        [JsonProperty("lineItems")]
        public GraphQlLineItemsConnection LineItems { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("note")]
        public string Note { get; set; }

        [JsonProperty("number")]
        public int? OrderNumber { get; set; }

        [JsonProperty("refunds")]
        public IEnumerable<GraphQlRefund> Refunds { get; set; }

        [JsonProperty("shippingLines")]
        public GraphQlShippingLineConnection ShippingLines { get; set; }

        [JsonProperty("shippingAddress")]
        public GraphQlAddress ShippingAddress { get; set; }

        [JsonProperty("subtotalPriceSet")]
        public GraphQlMoneySet SubtotalPrice { get; set; }

        [JsonProperty("taxLines")]
        public IEnumerable<GraphQlTaxLine> TaxLines { get; set; }

        [JsonProperty("taxesIncluded")]
        public bool? TaxesIncluded { get; set; }

        [JsonProperty("totalDiscountsSet")]
        public GraphQlMoneySet TotalDiscounts { get; set; }

        [JsonProperty("totalPriceSet")]
        public GraphQlMoneySet TotalPrice { get; set; }

        [JsonProperty("transactions")]
        public IEnumerable<GraphQlTransaction> Transactions { get; set; }

        [JsonProperty("metafields")]
        public GraphQlMetafieldsConnection Metafields { get; set; }
    }

    public class GraphQlMetafieldsConnection
    {
        [JsonProperty("nodes")]
        public List<GraphQlMetafield> Nodes { get; set; }
    }

    public class GraphQlMetafield
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("namespace")]
        public string Namespace { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }

    public class GraphQlAddress
    {
        [JsonProperty("country")]
        public string Country { get; set; }
    }

    public class GraphQlTaxLine
    {
        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("priceSet")]
        public GraphQlMoneySet PriceSet { get; set; }
    }

    public class GraphQlDiscountApplicationsConnection
    {
        [JsonProperty("nodes")]
        public List<GraphQlDiscountApplication> Nodes { get; set; }
    }

    public class GraphQlDiscountApplication
    {
        [JsonProperty("targetType")]
        public string TargetType { get; set; }

        [JsonProperty("value")]
        public GraphQlDiscountValue Value { get; set; }
    }

    public class GraphQlDiscountValue
    {
        [JsonProperty("__typename")]
        public string TypeName { get; set; }

        [JsonProperty("amount")]
        public decimal? Amount { get; set; }

        [JsonProperty("currencyCode")]
        public string CurrencyCode { get; set; }

        [JsonProperty("percentage")]
        public decimal? Percentage { get; set; }
    }

    public class GraphQlCustomer
    {
        [JsonProperty("firstName")]
        public string FirstName { get; set; }

        [JsonProperty("lastName")]
        public string LastName { get; set; }
    }

    public class GraphQlShippingLineConnection
    {
        [JsonProperty("nodes")]
        public List<GraphQlShippingLine> Nodes { get; set; }
    }

    public class GraphQlShippingLine
    {
        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("discountedPriceSet")]
        public GraphQlMoneySet DiscountedPriceSet { get; set; }

        [JsonProperty("originalPriceSet")]
        public GraphQlMoneySet OriginalPriceSet { get; set; }
    }

    public class GraphQlRefund
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("createdAt")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonProperty("orderAdjustments")]
        public GraphQlOrderAdjustmentsConnection OrderAdjustments { get; set; }

        [JsonProperty("refundLineItems")]
        public GraphQlRefundLineItemsConnection RefundLineItems { get; set; }

        [JsonProperty("transactions")]
        public GraphQlTransactionsConnection Transactions { get; set; }

        [JsonProperty("refundShippingLines")]
        public GraphQlRefundShippingLinesConnection RefundShippingLines { get; set; }
    }

    public class GraphQlRefundShippingLinesConnection
    {
        [JsonProperty("nodes")]
        public List<GraphQlRefundShippingLine> Nodes { get; set; }
    }

    public class GraphQlRefundShippingLine
    {
        [JsonProperty("shippingLine")]
        public GraphQlShippingLine ShippingLine { get; set; }
    }

    public class GraphQlTransactionsConnection
    {
        [JsonProperty("nodes")]
        public List<GraphQlTransaction> Nodes { get; set; }
    }

    public class GraphQlOrderAdjustmentsConnection
    {
        [JsonProperty("nodes")]
        public List<GraphQlOrderAdjustment> Nodes { get; set; }
    }

    public class GraphQlRefundLineItemsConnection
    {
        [JsonProperty("nodes")]
        public List<GraphQlRefundLineItem> Nodes { get; set; }
    }

    public class GraphQlRefundLineItem
    {
        [JsonProperty("quantity")]
        public int Quantity { get; set; }

        [JsonProperty("restockType")]
        public string RestockType { get; set; }

        [JsonProperty("subtotalSet")]
        public GraphQlMoneySet SubtotalSet { get; set; }

        [JsonProperty("lineItem")]
        public GraphQlRefundSourceLineItem LineItem { get; set; }

        [JsonProperty("location")]
        public GraphQlLocation Location { get; set; }
    }

    public class GraphQlRefundSourceLineItem
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("taxable")]
        public bool Taxable { get; set; }

        [JsonProperty("sku")]
        public string Sku { get; set; }

        [JsonProperty("variant")]
        public GraphQlVariant Variant { get; set; }

        [JsonProperty("discountAllocations")]
        public List<GraphQlDiscountAllocation> DiscountAllocations { get; set; }

        [JsonProperty("originalUnitPriceSet")]
        public GraphQlMoneySet OriginalUnitPrice { get; set; }

        [JsonProperty("discountedUnitPriceSet")]
        public GraphQlMoneySet DiscountedUnitPrice { get; set; }
    }

    public class GraphQlLocation
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    }

    public class GraphQlOrderAdjustment
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("amountSet")]
        public GraphQlMoneySet AmountSet { get; set; }

        [JsonProperty("taxAmountSet")]
        public GraphQlMoneySet TaxAmountSet { get; set; }
    }

    public class GraphQlTransaction
    {
        [JsonProperty("createdAt")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonProperty("gateway")]
        public string Gateway { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("amountSet")]
        public GraphQlMoneySet AmountSet { get; set; }

        [JsonProperty("receiptJson")]
        public string ReceiptJson { get; set; }
    }

    public class GraphQlMoneySet
    {
        [JsonProperty("shopMoney")]
        public GraphQlMoney ShopMoney { get; set; }
    }

    public class GraphQlMoney
    {
        [JsonProperty("amount")]
        public decimal Amount { get; set; }

        [JsonProperty("currencyCode")]
        public string CurrencyCode { get; set; }
    }

    public class GraphQlLineItemsConnection
    {
        [JsonProperty("nodes")]
        public List<GraphQlLineItem> Nodes { get; set; }
    }

    public class GraphQlLineItem
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("quantity")]
        public int Quantity { get; set; }

        [JsonProperty("taxable")]
        public bool Taxable { get; set; }

        [JsonProperty("sku")]
        public string Sku { get; set; }

        [JsonProperty("vendor")]
        public string Vendor { get; set; }

        [JsonProperty("isGiftCard")]
        public bool? GiftCard { get; set; }

        [JsonProperty("fulfillmentService")]
        public GraphQlFulfillmentService FulfillmentService { get; set; }

        [JsonProperty("fulfillmentStatus")]
        public string FulfillmentStatus { get; set; }

        [JsonProperty("variant")]
        public GraphQlVariant Variant { get; set; }

        [JsonProperty("discountAllocations")]
        public List<GraphQlDiscountAllocation> DiscountAllocations { get; set; }

        [JsonProperty("originalUnitPriceSet")]
        public GraphQlMoneySet OriginalUnitPrice { get; set; }
    }

    public class GraphQlInventoryItem
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    }

    public class GraphQlVariant
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("price")]
        public decimal? Price { get; set; }

        [JsonProperty("sku")]
        public string Sku { get; set; }

        [JsonProperty("product")]
        public GraphQlProduct Product { get; set; }

        [JsonProperty("inventoryItem")]
        public GraphQlInventoryItem InventoryItem { get; set; }
    }

    public class GraphQlProduct
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    }

    public class GraphQlDiscountAllocation
    {
        [JsonProperty("allocatedAmount")]
        public GraphQlMoney AllocatedAmount { get; set; }

        [JsonProperty("allocatedAmountSet")]
        public GraphQlMoneySet AllocatedAmountSet { get; set; }
    }

    public class GraphQlFulfillmentService
    {
        [JsonProperty("serviceName")]
        public string ServiceName { get; set; }
    }

    public class GraphQlPageInfo
    {
        [JsonProperty("hasNextPage")]
        public bool HasNextPage { get; set; }

        [JsonProperty("endCursor")]
        public string EndCursor { get; set; }
    }
}