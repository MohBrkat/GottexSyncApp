using System;
using System.Collections.Generic;
using System.Linq;
using ShopifySharp;
using SyncApp.Models;
using SyncApp.Models.GraphQlDTOs;
using SyncAppCommon.Models.GraphQlDTOs;
using Transaction = ShopifySharp.Transaction;

namespace SyncAppCommon.Helpers
{
    public static class ShopifyGraphQlHelper
    {
        #region Orders
        public static string ConstructGraphQlQuery(
            DateTime? dateFrom,
            DateTime? dateTo,
            string financialStatus,
            string status,
            DateTime? updatedAtFrom = null,
            int pageSize = 150,
            string afterCursor = null
        )
        {
            var filterList = new List<string>();
            if (dateFrom.HasValue)
            {
                filterList.Add($"created_at:>={dateFrom.Value.AbsoluteStart():yyyy-MM-ddTHH:mm:sszzz}");
            }

            if (dateTo.HasValue)
            {
                filterList.Add($"created_at:<={dateTo.Value.AbsoluteEnd():yyyy-MM-ddTHH:mm:sszzz}");
            }

            if (!string.IsNullOrWhiteSpace(financialStatus))
            {
                filterList.Add($"(financial_status:{financialStatus})");
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                filterList.Add($"(status:{status})");
            }

            if (updatedAtFrom.HasValue)
            {
                filterList.Add($"updated_at:>={updatedAtFrom.Value.AbsoluteStart():yyyy-MM-ddTHH:mm:sszzz}");
            }

            var queryFilter = string.Join(" AND ", filterList);

            var afterClause = string.IsNullOrEmpty(afterCursor)
                ? string.Empty
                : $@", after: ""{afterCursor}""";

            return @"{
              orders(
                first: " + pageSize + @",
                query: """ + queryFilter + @"""" +
                afterClause + @"
                ) {
                nodes {
                      id
                      createdAt
                      customer {
                        firstName
                        lastName
                      }
                      customAttributes {
                        key
                        value
                      }
                      discountApplications(first: 10) {
                        nodes {
                          targetType
                          value {
                            __typename
                            ... on MoneyV2 {
                              amount
                              currencyCode
                            }
                            ... on PricingPercentageValue {
                              percentage
                            }
                          }
                        }
                      }
                    displayFinancialStatus
                    displayFulfillmentStatus
                    tags
                    lineItems(first: 25) {
                      nodes {
                        id
                        quantity
                        taxable
                        sku
                        vendor
                        isGiftCard
                        fulfillmentStatus
                        fulfillmentService {
                          serviceName
                        }
                        variant {
                          id
                          price
                          sku
                          product {
                            id
                          }
                          inventoryItem {
                            id
                          }
                        }
                        originalUnitPriceSet {
                          shopMoney {
                            amount
                            currencyCode
                          }
                        }
                        discountAllocations {
                          allocatedAmount {
                            amount
                            currencyCode
                          }
                          allocatedAmountSet {
                            shopMoney {
                              amount
                            }
                          }
                        }
                      }
                    }
                    name
                    note
                    number
                    refunds {
                      id
                      createdAt
                      orderAdjustments(first: 15) {
                        nodes {
                          id
                          reason

                          amountSet {
                            shopMoney {
                              amount
                              currencyCode
                            }
                          }

                          taxAmountSet {
                            shopMoney {
                              amount
                              currencyCode
                            }
                          }
                        }
                      }
                      refundLineItems(first: 25) {
                        nodes {
                          quantity
                          restockType
                          subtotalSet {
                            shopMoney {
                              amount
                              currencyCode
                            }
                          }

                          lineItem {
                            id
                            taxable
                            sku
                            variant {
                              id
                              price
                              sku
                              product {
                                id
                              }
                              inventoryItem {
                                id
                              }
                            }
                            originalUnitPriceSet {
                              shopMoney {
                                amount
                                currencyCode
                              }
                            }
                            discountedUnitPriceSet {
                              shopMoney {
                                amount
                              }
                            }
                            discountAllocations {
                              allocatedAmount {
                                amount
                                currencyCode
                              }
                              allocatedAmountSet {
                                shopMoney {
                                  amount
                                }
                              }
                            }
                          }
                          location {
                            id
                          }
                        }
                      }

                      transactions(first: 20) {
                        nodes {
                          id
                          createdAt
                          gateway
                          kind
                          status

                          amountSet {
                            shopMoney {
                              amount
                              currencyCode
                            }
                          }
                          receiptJson
                        }
                      }

                      refundShippingLines(first: 15) {
                        nodes {
                            shippingLine {
                                id
                                title
                                code

                                originalPriceSet {
                                    shopMoney {
                                        amount
                                        currencyCode
                                    }
                                }

                                discountedPriceSet {
                                    shopMoney {
                                        amount
                                        currencyCode
                                    }
                                }
                            }
                        }
                      }

                    }

                    shippingAddress {
                      country
                    }

                    shippingLines(first: 20) {
                      nodes {
                        title
                        code

                        discountedPriceSet {
                          shopMoney {
                            amount
                            currencyCode
                          }
                        }

                        originalPriceSet {
                          shopMoney {
                            amount
                            currencyCode
                          }
                        }
                      }
                    }

                    subtotalPriceSet {
                      shopMoney {
                        amount
                        currencyCode
                      }
                    }

                    taxLines {
                      title
                      priceSet {
                        shopMoney {
                          amount
                          currencyCode
                        }
                      }
                    }

                    taxesIncluded
                   
                    totalDiscountsSet {
                      shopMoney {
                        amount
                        currencyCode
                      }
                    }

                    totalPriceSet {
                      shopMoney {
                        amount
                        currencyCode
                      }
                    }

                    transactions(first: 20) {
                        id
                        createdAt
                        gateway
                        kind
                        status

                        amountSet {
                            shopMoney {
                            amount
                            currencyCode
                            }
                        }

                        receiptJson
                    }

                    metafields(first: 10) {
                      nodes {
                        id
                        namespace
                        key
                        value
                        type
                      }
                    }

                }

                pageInfo {
                    hasNextPage
                    endCursor
                }
            }
        }";
        }

        public static Order Map(GraphQlOrder source)
        {
            if (source == null)
                return null;

            return new Order
            {
                Id = ParseNullableId(source.Id),
                CreatedAt = source.CreatedAt?.ToLocalTime(),
                NoteAttributes = MapNoteAttributes(source.CustomAttributes),

                Customer = MapCustomer(source.Customer),
                DiscountCodes = MapDiscountCodes(source.DiscountApplications),

                FinancialStatus = source.DisplayFinancialStatus?.ToLowerInvariant(),
                FulfillmentStatus = MapFulfillmentStatus(source.DisplayFulfillmentStatus),
                Tags = string.Join(",", source.Tags ?? Array.Empty<string>()),
                LineItems = MapLineItems(source.LineItems),

                Name = source.Name,
                Note = source.Note,
                OrderNumber = source.OrderNumber,
                Refunds = MapRefunds(source.Refunds),

                ShippingLines = MapShippingLines(source.ShippingLines),
                ShippingAddress = MapShippingAddress(source.ShippingAddress),
                SubtotalPrice = source.SubtotalPrice?.ShopMoney?.Amount ?? 0,
                TaxLines = MapTaxLines(source.TaxLines),
                TaxesIncluded = source.TaxesIncluded,

                TotalDiscounts = source.TotalDiscounts?.ShopMoney?.Amount ?? 0,
                TotalPrice = source.TotalPrice?.ShopMoney?.Amount ?? 0,
                Transactions = MapTransactions(source.Transactions),
                Metafields = MapMetafields(source.Metafields)
            };
        }

        private static IEnumerable<NoteAttribute> MapNoteAttributes(GraphQlCustomAttribute[] customAttributes)
        {
            if (customAttributes == null)
                return Enumerable.Empty<NoteAttribute>();

            return customAttributes.Select(x => new NoteAttribute
            {
                Name = x.Key,
                Value = x.Value
            }).ToList();
        }

        private static IEnumerable<MetaField> MapMetafields(
            GraphQlMetafieldsConnection connection)
        {
            if (connection?.Nodes == null)
                return Enumerable.Empty<MetaField>();

            return connection.Nodes.Select(x => new MetaField
            {
                Id = ParseNullableId(x.Id),
                Namespace = x.Namespace,
                Key = x.Key,
                Value = x.Value,
                ValueType = x.Type
            }).ToList();
        }

        private static Address MapShippingAddress(GraphQlAddress shippingAddress)
        {
            if (shippingAddress == null)
                return null;

            return new Address
            {
                Country = shippingAddress.Country
            };
        }

        private static IEnumerable<Transaction> MapTransactions(IEnumerable<GraphQlTransaction> transactions)
        {
            if (transactions == null)
                return null;

            return transactions.Select(x => new Transaction
            {
                Amount = x.AmountSet?.ShopMoney?.Amount,
                CreatedAt = x.CreatedAt?.ToLocalTime(),
                Gateway = x.Gateway,
                Kind = x.Kind?.ToLowerInvariant(),
                Receipt = x.ReceiptJson,
                Status = x.Status?.ToLowerInvariant(),
                Currency = x.AmountSet?.ShopMoney?.CurrencyCode
            }).ToList();
        }

        private static IEnumerable<TaxLine> MapTaxLines(IEnumerable<GraphQlTaxLine> taxLines)
        {
            if (taxLines == null)
                return null;

            return taxLines.Select(x => new TaxLine
            {
                Price = x.PriceSet?.ShopMoney?.Amount,
                Title = x.Title
            }).ToList();
        }

        private static IEnumerable<Refund> MapRefunds(
            IEnumerable<GraphQlRefund> refunds)
        {
            if (refunds == null)
                return new List<Refund>();

            return refunds.Select(MapRefund).ToList();
        }

        private static IEnumerable<DiscountCode> MapDiscountCodes(GraphQlDiscountApplicationsConnection connection)
        {
            var result = new List<DiscountCode>();

            if (connection?.Nodes == null)
                return result;

            foreach (var application in connection.Nodes)
            {
                result.Add(new DiscountCode
                {
                    Type = MapDiscountCodeType(application.TargetType),
                    Amount = application.Value?.Amount?.ToString(),
                });
            }

            return result;
        }

        private static string MapDiscountCodeType(string targetType)
        {
            switch (targetType)
            {
                case "SHIPPING_LINE":
                    return "shipping";
                default:
                    return targetType?.ToLowerInvariant();
            }
        }

        private static Customer MapCustomer(GraphQlCustomer customer)
        {
            if (customer == null)
                return null;

            return new Customer
            {
                FirstName = customer.FirstName,
                LastName = customer.LastName,
            };
        }

        private static long? ParseNullableId(string gid)
        {
            if (string.IsNullOrWhiteSpace(gid))
                return null;

            var lastPart = gid.Split('/').LastOrDefault();

            if (long.TryParse(lastPart, out long id))
                return id;

            return null;
        }

        private static string MapFulfillmentStatus(string status)
        {
            switch (status)
            {
                case "FULFILLED":
                    return "fulfilled";
                case "PARTIALLY_FULFILLED":
                    return "partial";
                case "UNFULFILLED":
                    return null;
                default:
                    return status?.ToLowerInvariant();
            }
        }

        private static List<LineItem> MapLineItems(
            GraphQlLineItemsConnection connection)
        {
            if (connection?.Nodes == null)
                return new List<LineItem>();

            return connection.Nodes
                .Select(MapLineItem)
                .ToList();
        }

        private static LineItem MapLineItem(GraphQlLineItem source)
        {
            if (source == null)
                return null;

            return new LineItem
            {
                Id = ParseNullableId(source.Id),
                FulfillmentService = source.FulfillmentService?.ServiceName?.ToLowerInvariant(),
                FulfillmentStatus = source.FulfillmentStatus,
                Price = source.OriginalUnitPrice?.ShopMoney?.Amount ?? source.Variant?.Price,
                ProductId = ParseNullableId(source.Variant?.Product?.Id),
                Quantity = source.Quantity,
                SKU = source.Sku ?? source.Variant?.Sku,
                VariantId = ParseNullableId(source.Variant?.Id),
                Vendor = source.Vendor,
                GiftCard = source.GiftCard,
                Taxable = source.Taxable,
                DiscountAllocations = source.DiscountAllocations?.Select(x =>
                new DiscountAllocation()
                {
                    Amount = x.AllocatedAmount?.Amount.ToString()
                }).ToList(),
                InventoryItemId = ParseNullableId(source.Variant?.InventoryItem?.Id),
            };
        }


        private static List<ShippingLine> MapShippingLines(
            GraphQlShippingLineConnection source)
        {
            if (source?.Nodes == null)
                return new List<ShippingLine>();

            return source.Nodes.Select(x => new ShippingLine
            {
                Code = x.Code,

                Title = x.Title,

                Price = x.OriginalPriceSet?.ShopMoney?.Amount,
                DiscountedPrice = x.DiscountedPriceSet?.ShopMoney?.Amount
            }).ToList();
        }

        private static Refund MapRefund(GraphQlRefund source)
        {
            if (source == null)
                return null;

            return new Refund
            {
                Id = ParseNullableId(source.Id),
                CreatedAt = source.CreatedAt?.ToLocalTime(),
                OrderAdjustments = MapOrderAdjustments(source.OrderAdjustments),

                RefundLineItems = MapRefundLineItems(source.RefundLineItems),

                Transactions = MapRefundTransactions(source.Transactions),

                RefundShippingLines = MapRefundShippingLines(source.RefundShippingLines),
            };
        }

        private static IEnumerable<ShippingLine> MapRefundShippingLines(GraphQlRefundShippingLinesConnection refundShippingLines)
        {
            if (refundShippingLines?.Nodes == null)
            {
                return new List<ShippingLine>();
            }

            return refundShippingLines.Nodes
                .Where(x => x.ShippingLine != null)
                .Select(x => new ShippingLine
                {
                    Code = x.ShippingLine.Code,
                    Title = x.ShippingLine.Title,
                    Price = x.ShippingLine.OriginalPriceSet?.ShopMoney?.Amount,
                    DiscountedPrice = x.ShippingLine.DiscountedPriceSet?.ShopMoney?.Amount
                })
                .ToList();
        }

        private static IEnumerable<RefundOrderAdjustment> MapOrderAdjustments(
            GraphQlOrderAdjustmentsConnection connection)
        {
            if (connection?.Nodes == null)
                return new List<RefundOrderAdjustment>();

            return connection.Nodes.Select(x => new RefundOrderAdjustment
            {
                Id = ParseNullableId(x.Id),
                Amount = x.AmountSet?.ShopMoney?.Amount,
                Kind = x.Reason,
                TaxAmount = x.TaxAmountSet?.ShopMoney?.Amount,
            }).ToList();
        }

        private static IEnumerable<RefundLineItem> MapRefundLineItems(
            GraphQlRefundLineItemsConnection connection)
        {
            if (connection?.Nodes == null)
                return new List<RefundLineItem>();

            return connection.Nodes.Select(x => new RefundLineItem
            {
                LineItemId = ParseNullableId(x.LineItem?.Id),

                Quantity = x.Quantity,

                SubTotal = x.SubtotalSet?.ShopMoney?.Amount,

                LineItem = new LineItem
                {
                    Id = ParseNullableId(x.LineItem?.Id),
                    SKU = x.LineItem?.Sku,
                    DiscountAllocations = x.LineItem?.DiscountAllocations?.Select(d =>
                    new DiscountAllocation()
                    {
                        Amount = d.AllocatedAmount?.Amount.ToString()
                    }).ToList(),
                    Price = x.LineItem?.OriginalUnitPrice?.ShopMoney?.Amount,
                    Taxable = x.LineItem?.Taxable,
                    ProductId = ParseNullableId(x.LineItem?.Variant?.Product?.Id),
                    InventoryItemId = ParseNullableId(x.LineItem?.Variant?.InventoryItem?.Id),
                },

                LocationId = ParseNullableId(x.Location?.Id),
            }).ToList();
        }

        private static IEnumerable<Transaction> MapRefundTransactions(
            GraphQlTransactionsConnection connection)
        {
            if (connection?.Nodes == null)
                return new List<Transaction>();

            return connection.Nodes.Select(x => new Transaction
            {
                Amount = x.AmountSet?.ShopMoney?.Amount,

                CreatedAt = x.CreatedAt?.ToLocalTime(),

                Gateway = x.Gateway,

                Kind = x.Kind?.ToLowerInvariant(),

                Receipt = x.ReceiptJson,

                Status = x.Status?.ToLowerInvariant(),

                Currency = x.AmountSet?.ShopMoney?.CurrencyCode
            }).ToList();
        }
        #endregion

        #region Inventory
        public static string ConstructInventoryItemsQuery(
            IEnumerable<long> inventoryItemIds)
        {
            var gids = inventoryItemIds
                .Distinct()
                .Select(id => $"\"gid://shopify/InventoryItem/{id}\"").ToList();

            return @"{
                        nodes(ids: [" + string.Join(",", gids) + @"]) {
                            ... on InventoryItem {
                                id

                                inventoryLevels(first: 20) {
                                    nodes {
                                        location {
                                            id
                                            name
                                        }
                                    }
                                }
                            }
                        }
                    }";
        }

        public static Dictionary<long, List<long>> BuildInventoryLocationLookup(
            IEnumerable<GraphQlInventoryItemNode> inventoryItems)
        {
            var result =
                new Dictionary<long, List<long>>();

            foreach (var item in inventoryItems ?? Enumerable.Empty<GraphQlInventoryItemNode>())
            {
                var inventoryItemId =
                    ParseNullableId(item.Id);

                if (!inventoryItemId.HasValue)
                {
                    continue;
                }

                var locationIds =
                    item.InventoryLevels?.Nodes?
                        .Select(x => ParseNullableId(x.Location?.Id))
                        .Where(x => x.HasValue)
                        .Select(x => x.Value)
                        .Distinct()
                        .ToList()
                    ?? new List<long>();

                result[inventoryItemId.Value] = locationIds;
            }

            return result;
        }
        #endregion

        #region Products
        public static string ConstructProductsQuery(
            int pageSize = 250,
            string afterCursor = null)
        {
            var afterClause = string.IsNullOrWhiteSpace(afterCursor)
                ? string.Empty
                : $@", after: ""{afterCursor}""";

            return @"{
                      products(
                        first: " + pageSize + @"
                        " + afterClause + @"
                      ) {
                        nodes {
                          id
                          vendor

                          variants(first: 250) {
                            nodes {
                              id
                              sku
                              barcode

                              inventoryItem {
                                id
                              }
                            }
                          }
                        }

                        pageInfo {
                          hasNextPage
                          endCursor
                        }
                      }
                    }";
        }

        public static Product MapProduct(
            GraphQlProduct source)
        {
            return new Product
            {
                Id = ParseNullableId(source.Id),

                Vendor = source.Vendor,

                Variants =
                    source.Variants?.Nodes?
                        .Select(MapVariant)
                        .ToList()
                        ?? new List<ProductVariant>()
            };
        }

        private static ProductVariant MapVariant(
            GraphQlProductVariant source)
        {
            if (source == null)
            {
                return null;
            }

            return new ProductVariant
            {
                Id = ParseNullableId(source.Id),

                SKU = source.Sku,

                InventoryItemId =
                    ParseNullableId(
                        source.InventoryItem?.Id),

                Barcode = source.Barcode,
            };
        }
        #endregion
    }
}