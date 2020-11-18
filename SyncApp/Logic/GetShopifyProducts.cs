using ShopifySharp;
using ShopifySharp.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncApp.Logic
{
    public class GetShopifyProducts
    {
        private string _storeUrl;
        private string _apiSecret;
        public GetShopifyProducts(string storeUrl, string apiSecret)
        {
            _storeUrl = storeUrl;
            _apiSecret = apiSecret;
        }

        public async Task<List<Product>> GetProductsAsync()
        {
            List<Product> products = new List<Product>();

            var productServices = new ProductService(_storeUrl, _apiSecret);

            var filter = new ProductListFilter
            {
                Limit = 250,
                Fields = "id,handle,vendor,Variants"
            };

            var page = await productServices.ListAsync(filter);

            while (true)
            {
                products.AddRange(page.Items);

                if (!page.HasNextPage)
                {
                    break;
                }

                try
                {
                    page = await productServices.ListAsync(page.GetNextPageFilter());
                }
                catch (ShopifyRateLimitException)
                {
                    await Task.Delay(10000);

                    page = await productServices.ListAsync(page.GetNextPageFilter());
                }
            }

            return products;
        }
    }
}