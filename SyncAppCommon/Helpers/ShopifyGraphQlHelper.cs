using System;
using System.Collections.Generic;
using System.Linq;
using ShopifySharp;
using SyncApp.Models.GraphQlDTOs;
using SyncAppEntities.Models;
using Transaction = ShopifySharp.Transaction;

namespace SyncAppCommon.Helpers
{
    public static class ShopifyGraphQlHelper
    {
        public static string ConstructGraphQlQuery(
            DateTime dateFrom,
            DateTime? dateTo,
            string financialStatus,
            int pageSize = 150,
            string afterCursor = null
            )
        {
            var queryFilter = $"created_at:>={dateFrom.AbsoluteStart():yyyy-MM-ddTHH:mm:sszzz} ";

            if (dateTo.HasValue == true)
            {
                queryFilter += $"AND created_at:<={dateTo.Value.AbsoluteEnd():yyyy-MM-ddTHH:mm:sszzz} ";
            }

            if (!string.IsNullOrWhiteSpace(financialStatus))
            {
                queryFilter += $"AND (financial_status:{financialStatus})";
            }

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
                      discountCodes
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
                    lineItems(first: 50) {
                      nodes {
                        id
                        title
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
                        }
                      }
                    }
                    name
                    note
                    number
                    refunds {
                      createdAt
                      orderAdjustments(first: 50) {
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
                      refundLineItems(first: 50) {
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
                            sku
                          }
                          location {
                            id
                          }
                        }
                      }

                      transactions(first: 50) {
                        nodes {
                          id
                          kind
                          status

                          amountSet {
                            shopMoney {
                              amount
                              currencyCode
                            }
                          }
                        }
                      }
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
                      rate
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

                    totalTaxSet {
                      shopMoney {
                        amount
                        currencyCode
                      }
                    }

                    transactions(first: 50) {
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
                Customer = MapCustomer(source.Customer),
                DiscountCodes = MapDiscountCodes(source.DiscountApplications),

                FinancialStatus = source.DisplayFinancialStatus?.ToLowerInvariant(),
                FulfillmentStatus = MapFulfillmentStatus(source.DisplayFulfillmentStatus),
                Tags = string.Join(",", source.Tags),
                LineItems = MapLineItems(source.LineItems),

                Name = source.Name,
                Note = source.Note,
                OrderNumber = source.OrderNumber,
                Refunds = MapRefunds(source.Refunds),

                ShippingLines = MapShippingLines(source.ShippingLines),
                SubtotalPrice = source.SubtotalPrice?.ShopMoney?.Amount ?? 0,
                TaxLines = MapTaxLines(source.TaxLines),
                TaxesIncluded = source.TaxesIncluded,

                TotalDiscounts = source.TotalDiscounts?.ShopMoney?.Amount ?? 0,
                TotalPrice = source.TotalPrice?.ShopMoney?.Amount ?? 0,
                Transactions = MapTransactions(source.Transactions)
            };
        }

        private static IEnumerable<Transaction> MapTransactions(IEnumerable<GraphQlTransaction> transactions)
        {
            if (transactions == null)
                return null;

            return transactions.Select(x => new Transaction
            {
                Amount = x.AmountSet?.ShopMoney?.Amount,
                CreatedAt = x.CreatedAt,
                Gateway = x.Gateway,
                Kind = x.Kind,
                Receipt = x.ReceiptJson,
                Status = x.Status,
                Currency = x.AmountSet?.ShopMoney?.CurrencyCode
            });
        }

        private static IEnumerable<TaxLine> MapTaxLines(IEnumerable<GraphQlTaxLine> taxLines)
        {
            if (taxLines == null)
                return null;

            return taxLines.Select(x => new TaxLine
            {
                Price = x.PriceSet?.ShopMoney?.Amount,
                Title = x.Title
            });
        }

        private static IEnumerable<Refund> MapRefunds(
            IEnumerable<GraphQlRefund> refunds)
        {
            if (refunds == null)
                return new List<Refund>();

            return refunds.Select(MapRefund);
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
            return targetType switch
            {
                "SHIPPING_LINE" => "shipping",
                _ => targetType?.ToLowerInvariant(),
            };
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
            return status switch
            {
                "FULFILLED" => "fulfilled",
                "PARTIALLY_FULFILLED" => "partial",
                "UNFULFILLED" => "unfulfilled",
                _ => status?.ToLowerInvariant(),
            };
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
                })
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
                CreatedAt = source.CreatedAt,
                OrderAdjustments = MapOrderAdjustments(source.OrderAdjustments),

                RefundLineItems = MapRefundLineItems(source.RefundLineItems),

                Transactions = MapRefundTransactions(source.Transactions),

                Restock = HasRestockedItems(source)
            };
        }

        private static bool HasRestockedItems(GraphQlRefund refund)
        {
            return refund?.RefundLineItems?.Nodes?
                .Any(x => x.RestockType != "NO_RESTOCK") == true;
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
            });
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
                    SKU = x.LineItem?.Sku
                },

                LocationId = ParseNullableId(x.Location?.Id),
            });
        }

        private static IEnumerable<Transaction> MapRefundTransactions(
            GraphQlTransactionsConnection connection)
        {
            if (connection?.Nodes == null)
                return new List<Transaction>();

            return connection.Nodes.Select(x => new Transaction
            {
                Amount = x.AmountSet?.ShopMoney?.Amount,

                CreatedAt = x.CreatedAt,

                Gateway = x.Gateway,

                Kind = x.Kind,

                Receipt = x.ReceiptJson,

                Status = x.Status,

                Currency = x.AmountSet?.ShopMoney?.CurrencyCode
            });
        }
    }
}
