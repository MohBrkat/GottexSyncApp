using ShopifySharp;
using ShopifySharp.Filters;
using SyncApp.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncApp.Logic
{
    public class GetShopifyOrders
    {
        private string _storeUrl;
        private string _apiSecret;
        public GetShopifyOrders(string storeUrl, string apiSecret)
        {
            _storeUrl = storeUrl;
            _apiSecret = apiSecret;
        }

        public async Task<List<Order>> GetNotExportedOrdersAsync(DateTime dateFrom = default, DateTime dateTo = default)
        {
            await Task.Delay(1000);
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

            var filter = new OrderListFilter
            {
                FinancialStatus = "any",
                Status = "any",
                FulfillmentStatus = "any",
                CreatedAtMin = dateFrom.AbsoluteStart(),
                CreatedAtMax = dateTo.AbsoluteEnd()
            };

            List<Order> orders = await GetNotExportedOrderByFiltersAsync(filter);

            return orders;
        }
        private async Task<List<Order>> GetNotExportedOrderByFiltersAsync(OrderListFilter filter)
        {
            List<Order> Orders = new List<Order>();

            var paidFinancialFilter = new OrderListFilter
            {
                FinancialStatus = "paid",
                Status = filter.Status,
                FulfillmentStatus = filter.FulfillmentStatus,
                CreatedAtMin = filter.CreatedAtMin,
                CreatedAtMax = filter.CreatedAtMax
            };

            var refundedFinancialFilter = new OrderListFilter
            {
                FinancialStatus = "refunded",
                Status = filter.Status,
                FulfillmentStatus = filter.FulfillmentStatus,
                CreatedAtMin = filter.CreatedAtMin,
                CreatedAtMax = filter.CreatedAtMax
            };

            var partiallyRefundedFinancialFilter = new OrderListFilter
            {
                FinancialStatus = "partially_refunded",
                Status = filter.Status,
                FulfillmentStatus = filter.FulfillmentStatus,
                CreatedAtMin = filter.CreatedAtMin,
                CreatedAtMax = filter.CreatedAtMax
            };

            Orders.AddRange(await GetOrderByFiltersAsync(paidFinancialFilter));
            Orders.AddRange(await GetOrderByFiltersAsync(refundedFinancialFilter));
            Orders.AddRange(await GetOrderByFiltersAsync(partiallyRefundedFinancialFilter));

            return Orders;
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

            List<Order> orders = await GetRefundedOrdersByFiltersAsync();

            var OrdersHasRefunds = orders.Where(a => a.Refunds.Count() > 0);

            var ordersToReturn = new ConcurrentBag<Order>();
            decimal taxPercentage = taxPercent;

            foreach(var order in OrdersHasRefunds)
            {
                var targetRefunds = order.Refunds.Where(a => a.CreatedAt.GetValueOrDefault().Date >= dateFrom.AbsoluteStart() &&
a.CreatedAt.GetValueOrDefault().Date <= dateTo.AbsoluteEnd()).ToList();

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
                        ShippingLines = order.ShippingLines
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
                            ProductId = itemRefund.LineItem.ProductId,
                            LocationId = itemRefund.LocationId
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
                        orderToReturn.RefundKind = refund.OrderAdjustments.First().Kind;
                    }

                    ordersToReturn.Add(orderToReturn);
                }
            }

            refundedOrders.Orders = ordersToReturn.ToList();

            return refundedOrders;
        }

        public async Task<List<Order>> GetRefundedOrdersByFiltersAsync()
        {
            List<Order> Orders = new List<Order>();

            var refundedFilter = new OrderListFilter
            {
                FinancialStatus = "refunded",
                Status = "any",
                FulfillmentStatus = "any"
            };

            var partiallyRefundedFilter = new OrderListFilter
            {
                FinancialStatus = "partially_refunded",
                Status = "any",
                FulfillmentStatus = "any"
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
    }
}
