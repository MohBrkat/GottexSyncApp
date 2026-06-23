using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShopifySharp;
using ShopifySharp.Filters;
using SyncApp.Models;
using SyncApp.Models.EF;
using SyncApp.Models.GraphQlDTOs;
using SyncAppCommon.Helpers;
using SyncAppCommon.Models.GraphQlDTOs;

namespace SyncApp.Logic
{
    public class GetShopifyOrders
    {
        private string _storeUrl;
        private string _apiSecret;
        private readonly ShopifyAppContext _context;

        public GetShopifyOrders(string storeUrl, string apiSecret, ShopifyAppContext context)
        {
            _storeUrl = storeUrl;
            _apiSecret = apiSecret;
            _context = context;
        }

        private Configrations Config
        {
            get
            {
                return _context.Configrations.First();
            }
        }

        private int RefundOrdersHistoryDays
        {
            get
            {
                return Config.RefundOrdersHistoryDays.GetValueOrDefault();
            }
        }

        public async Task<List<Order>> GetNotExportedOrdersAsync(DateTime dateFrom = default, DateTime dateTo = default)
        {
            await Task.Delay(1000);

            if (dateFrom == default && dateTo == default) //Yesterday option (Default)
            {
                dateFrom = DateTime.Now.AddDays(-1); // by default
                dateTo = DateTime.Now.AddDays(-1);
            }

            dateFrom = dateFrom.Date.AddDays(-1);
            dateTo = dateTo.Date;

            OrderListFilter filter = new OrderListFilter();

            if (dateFrom != default && dateTo == default)
            {
                filter = new OrderListFilter
                {
                    FinancialStatus = "any",
                    Status = "any",
                    FulfillmentStatus = "any",
                    CreatedAtMin = dateFrom.AbsoluteStart()
                };
            }
            else if (dateFrom == default && dateTo != default)
            {
                filter = new OrderListFilter
                {
                    FinancialStatus = "any",
                    Status = "any",
                    FulfillmentStatus = "any",
                    CreatedAtMax = dateTo.AbsoluteEnd()
                };
            }
            else
            {
                filter = new OrderListFilter
                {
                    FinancialStatus = "any",
                    Status = "any",
                    FulfillmentStatus = "any",
                    CreatedAtMin = dateFrom.AbsoluteStart(),
                    CreatedAtMax = dateTo.AbsoluteEnd()
                };

            }

            List<Order> orders = await GetNotExportedOrderByFiltersAsync(filter);

            return orders;
        }
        
        private async Task<List<Order>> GetNotExportedOrderByFiltersAsync(OrderListFilter filter)
        {
            var result = new List<Order>();

            var financialStatuses = new[]
            {
                "paid",
                "refunded",
                "partially_refunded"
            };

            foreach (var status in financialStatuses)
            {
                var graphQlOrders = await GetGraphQlOrdersAsync(new OrderListFilter
                {
                    FinancialStatus = status,
                    Status = filter.Status,
                    FulfillmentStatus = filter.FulfillmentStatus,
                    CreatedAtMin = filter.CreatedAtMin,
                    CreatedAtMax = filter.CreatedAtMax
                });

                if (graphQlOrders == null || graphQlOrders.Count == 0)
                    continue;

                var mappedOrders = graphQlOrders
                    .Select(ShopifyGraphQlHelper.Map)
                    .ToList();

                result.AddRange(mappedOrders);
            }

            return result;
        }
        
        public async Task<RefundedOrders> GetRefundedOrdersAsync(DateTime dateFrom = default, DateTime dateTo = default, int taxPercent = 0)
        {
            var refundedOrders = new RefundedOrders();

            Dictionary<string, List<string>> lsOfTagsToBeAddedTemp = new Dictionary<string, List<string>>();

            if (dateFrom == default) //Yesterday option (Default)
            {
                dateFrom = DateTime.Now.AddDays(-1); // by default
                dateTo = DateTime.Now.AddDays(-1);
            }
            else if (dateTo == default) //Single day option
            {
                dateTo = dateFrom.Date;
            }

            // to trim hours and minutes, ...
            dateFrom = dateFrom.Date;
            dateTo = dateTo.Date;

            var orders = await GetGraphQlRefundedOrdersAsync(dateFrom);

            var OrdersHasRefunds = orders.Where(a => a.Refunds.Count() > 0);
            var ordersToReturn = new List<Order>();
            decimal taxPercentage = taxPercent;

            foreach (var order in OrdersHasRefunds)
            {
                var targetRefunds = order.Refunds.Where(a => a.CreatedAt.GetValueOrDefault() >= dateFrom.AbsoluteStart() &&
                a.CreatedAt.GetValueOrDefault() <= dateTo.AbsoluteEnd()).ToList();

                foreach (var refund in targetRefunds)
                {
                    var orderToReturn = new Order
                    {
                        TotalDiscounts = order.TotalDiscounts,
                        OrderNumber = order.OrderNumber,
                        Id = order.Id,
                        Tags = order.Tags,

                        SubtotalPrice = order.SubtotalPrice,
                        FinancialStatus = order.FinancialStatus,
                        ShippingLines = refund.RefundShippingLines,
                        ShippingAddress = order.ShippingAddress,

                        Refunds = new List<Refund>() { refund },
                        Metafields = order.Metafields,
                        OriginalTransactions = order.Transactions ?? new List<Transaction>(),
                    };

                    var refundLineItems = refund.RefundLineItems;

                    List<LineItem> lsOfLineItems = new List<LineItem>();

                    foreach (var itemRefund in refundLineItems)
                    {
                        lsOfLineItems.Add(new LineItem
                        {
                            Quantity = itemRefund.Quantity * -1,
                            Price = itemRefund.LineItem.Price,
                            SKU = itemRefund.LineItem.SKU,
                            Taxable = itemRefund.LineItem.Taxable,
                            Id = itemRefund.LineItem.Id,
                            DiscountAllocations = itemRefund.LineItem.DiscountAllocations,
                        });
                        foreach (var discount in lsOfLineItems.Last().DiscountAllocations)
                        {
                            List<LineItem> tt = order.LineItems.Where(a => a.Id == lsOfLineItems.Last().Id).ToList();
                            decimal quantity = (decimal)tt.First().Quantity;
                            discount.Amount = (decimal.Parse(discount.Amount) / quantity)
                                + "";
                        }
                    }

                    orderToReturn.CreatedAt = refund.CreatedAt;

                    orderToReturn.TaxesIncluded = false;

                    orderToReturn.LineItems = lsOfLineItems;

                    orderToReturn.TaxLines = order.TaxLines;

                    orderToReturn.TaxesIncluded = order.TaxesIncluded;

                    orderToReturn.Transactions = refund.Transactions;

                    var totalPrice = refund.Transactions.Sum(t => t.Amount);
                    decimal priceWithVat = (decimal)totalPrice / ((taxPercentage / 100.0m) + 1.0m);

                    orderToReturn.TotalTax = totalPrice - priceWithVat;
                    orderToReturn.TotalPrice = totalPrice;

                    var refundInfo = refund.OrderAdjustments;

                    orderToReturn.RefundKind = "refund_discrepancy";

                    if (refundInfo != null && refundInfo.Count() != 0)
                    {
                        orderToReturn.RefundAmount = (decimal)((refund.OrderAdjustments.First().Amount +
                                    refund.OrderAdjustments.First().TaxAmount));
                        if (refund?.RefundShippingLines?.Any() == true)
                        {
                            orderToReturn.RefundKind = "shipping_refund";
                        }
                        else
                        {
                            orderToReturn.RefundKind = "refund_discrepancy";
                        }
                    }

                    ordersToReturn.Add(orderToReturn);
                }
            }
            refundedOrders.Orders = ordersToReturn.ToList();

            return refundedOrders;
        }
        
        public async Task<List<Order>> GetRefundedOrdersByFiltersAsync(DateTime dateFrom, DateTime dateTo)
        {
            var refundOrderDays = RefundOrdersHistoryDays;
            List<Order> Orders = new List<Order>();

            var refundedFilter = new OrderListFilter
            {
                FinancialStatus = "refunded",
                Status = "any",
                FulfillmentStatus = "any",
                CreatedAtMin = dateFrom.AddDays(-refundOrderDays).AbsoluteStart()
            };

            var partiallyRefundedFilter = new OrderListFilter
            {
                FinancialStatus = "partially_refunded",
                Status = "any",
                FulfillmentStatus = "any",
                CreatedAtMin = dateFrom.AddDays(-refundOrderDays).AbsoluteStart()
            };

            Orders.AddRange(await GetOrderByFiltersAsync(refundedFilter));
            Orders.AddRange(await GetOrderByFiltersAsync(partiallyRefundedFilter));

            return Orders;
        }
        
        public List<Order> GetReportOrders(DateTime dateFrom = default, DateTime dateTo = default)
        {
            dateFrom = dateFrom.Date;
            dateTo = dateTo.Date;

            OrderListFilter filter = new OrderListFilter();

            if (dateFrom != default && dateTo == default)
            {
                filter = new OrderListFilter
                {
                    FinancialStatus = "paid",
                    Status = "open",
                    FulfillmentStatus = "any",
                    CreatedAtMin = dateFrom.AbsoluteStart()
                };
            }
            else if (dateFrom == default && dateTo != default)
            {
                filter = new OrderListFilter
                {
                    FinancialStatus = "paid",
                    Status = "open",
                    FulfillmentStatus = "any",
                    CreatedAtMax = dateTo.AbsoluteEnd()
                };
            }
            else if (dateFrom == default && dateTo == default)
            {
                filter = new OrderListFilter
                {
                    FinancialStatus = "paid",
                    Status = "open",
                    FulfillmentStatus = "any"
                };
            }
            else
            {
                filter = new OrderListFilter
                {
                    FinancialStatus = "paid",
                    Status = "open",
                    FulfillmentStatus = "any",
                    CreatedAtMin = dateFrom.AbsoluteStart(),
                    CreatedAtMax = dateTo.AbsoluteEnd()
                };

            }

            List<Order> orders = GetOrderByFiltersAsync(filter).Result.Select(a => a).Where(a => a.FulfillmentStatus == null ||
                    a.FulfillmentStatus == "partial").ToList();

            return orders;
        }
       
        public RefundedOrders GetReportRefundedOrders(DateTime dateFrom = default, DateTime dateTo = default)
        {
            var refundedOrders = new RefundedOrders();

            dateFrom = dateFrom.Date;
            dateTo = dateTo.Date;

            OrderListFilter filter = new OrderListFilter();

            if (dateFrom != default && dateTo == default)
            {
                filter = new OrderListFilter
                {
                    FinancialStatus = "partially_refunded",
                    Status = "open",
                    FulfillmentStatus = "any",
                    CreatedAtMin = dateFrom.AbsoluteStart()
                };
            }
            else if (dateFrom == default && dateTo != default)
            {
                filter = new OrderListFilter
                {
                    FinancialStatus = "partially_refunded",
                    Status = "open",
                    FulfillmentStatus = "any",
                    CreatedAtMax = dateTo.AbsoluteEnd()
                };
            }
            else if (dateFrom == default && dateTo == default)
            {
                filter = new OrderListFilter
                {
                    FinancialStatus = "partially_refunded",
                    Status = "open",
                    FulfillmentStatus = "any"
                };
            }
            else
            {
                filter = new OrderListFilter
                {
                    FinancialStatus = "partially_refunded",
                    Status = "open",
                    FulfillmentStatus = "any",
                    CreatedAtMin = dateFrom.AbsoluteStart(),
                    CreatedAtMax = dateTo.AbsoluteEnd()
                };

            }

            List<Order> orders = GetOrderByFiltersAsync(filter).Result.Select(a => a).Where(a => a.FulfillmentStatus == null || a.FulfillmentStatus == "partial").ToList();

            var OrdersHasRefunds = orders.Where(a => a.Refunds.Count() > 0);
            foreach (var order in OrdersHasRefunds)
            {
                List<long> lineItemsIds = new List<long>();
                foreach (var refund in order.Refunds)
                {
                    var refundLineItems = refund.RefundLineItems;
                    foreach (var r in refundLineItems)
                    {
                        lineItemsIds.Add(r.LineItem.Id.GetValueOrDefault());
                    }
                }
                order.LineItems = order.LineItems.Where(l => !lineItemsIds.Contains(l.Id.GetValueOrDefault())).ToList();
            }

            var returnOrders = OrdersHasRefunds.Where(o => o.LineItems.Any()).ToList();
            refundedOrders.Orders = returnOrders;
            return refundedOrders;
        }

        public async Task<List<Order>> GetOrderByFiltersAsync(OrderListFilter filter)
        {
            List<Order> Orders = new List<Order>();

            var orderService = new OrderService(_storeUrl, _apiSecret);
            filter.Limit = 250;

            var page = await orderService.ListAsync(filter);

            while (true)
            {
                Orders.AddRange(page.Items);

                if (!page.HasNextPage)
                {
                    break;
                }

                try
                {
                    page = await orderService.ListAsync(page.GetNextPageFilter());
                }
                catch (ShopifyRateLimitException)
                {
                    await Task.Delay(10000);

                    page = await orderService.ListAsync(page.GetNextPageFilter());
                }
            }

            return Orders;
        }

        public async Task<List<GraphQlOrder>> GetGraphQlOrdersAsync(OrderListFilter orderListFilter)
        {
            if (!orderListFilter.CreatedAtMin.HasValue)
            {
                throw new ArgumentException(
                    "CreatedAtMin is required.",
                    nameof(orderListFilter));
            }

            var orders = new List<GraphQlOrder>();

            var graphService = new GraphService(_storeUrl, _apiSecret);

            string cursor = null;
            bool hasNextPage = false;

            do
            {
                var query = ShopifyGraphQlHelper.ConstructGraphQlQuery(
                    orderListFilter.CreatedAtMin.Value.Date,
                    orderListFilter.CreatedAtMax?.Date,
                    orderListFilter.FinancialStatus,
                    orderListFilter.UpdatedAtMin?.Date,
                    28,
                    cursor);

                var response = await ExecuteOrdersQueryAsync(
                    graphService,
                    query);

                if (response?.Orders?.Nodes == null)
                {
                    return orders;
                }

                orders.AddRange(response.Orders.Nodes);

                var previousCursor = cursor;

                cursor = response.Orders.PageInfo?.EndCursor;
                hasNextPage = response.Orders.PageInfo?.HasNextPage ?? false;

                if (hasNextPage && cursor == previousCursor)
                {
                    break;
                }

            } while (hasNextPage);

            return orders;
        }

        public async Task<List<Order>> GetGraphQlRefundedOrdersAsync(DateTime dateFrom)
        {
            var filter = new OrderListFilter
            {
                CreatedAtMin = dateFrom,
                UpdatedAtMin = dateFrom.AbsoluteStart(),
            };

            var graphQlOrders = await GetGraphQlOrdersAsync(filter);

            return graphQlOrders
                .Select(ShopifyGraphQlHelper.Map)
                .ToList();
        }

        public async Task<List<GraphQlInventoryItemNode>> GetInventoryItemsAsync(
            IEnumerable<long> inventoryItemIds)
        {
            var ids = inventoryItemIds?
                .Distinct()
                .ToList();

            if (ids?.Any() != true)
            {
                return new List<GraphQlInventoryItemNode>();
            }

            var graphService = new GraphService(
                _storeUrl,
                _apiSecret);

            var query =
                ShopifyGraphQlHelper.ConstructInventoryItemsQuery(ids);

            var result = await graphService.PostAsync(query);

            var response =
                result.ToObject<InventoryItemsResponse>();

            return response?.Nodes ??
                   new List<GraphQlInventoryItemNode>();
        }

        private static async Task<OrdersResponse> ExecuteOrdersQueryAsync(
            GraphService graphService,
            string query)
        {
            try
            {
                var result = await graphService.PostAsync(query);

                return result.ToObject<OrdersResponse>();
            }
            catch (ShopifyRateLimitException)
            {
                await Task.Delay(10000);

                var result = await graphService.PostAsync(query);

                return result.ToObject<OrdersResponse>();
            }
        }
    }
}