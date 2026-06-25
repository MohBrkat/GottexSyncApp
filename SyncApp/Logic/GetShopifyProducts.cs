using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShopifySharp;
using ShopifySharp.Filters;
using SyncAppCommon.Helpers;
using SyncAppCommon.Models.GraphQlDTOs;

namespace SyncAppEntities.Logic
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

        public async Task<List<GraphQlProduct>> GetGraphQlProductsAsync()
        {
            var products = new List<GraphQlProduct>();

            var graphService =
                new GraphService(_storeUrl, _apiSecret);

            string cursor = null;
            bool hasNextPage;

            do
            {
                var query =
                    ShopifyGraphQlHelper.ConstructProductsQuery(
                        250,
                        cursor);

                var result =
                    await graphService.PostAsync(query);

                var response =
                    result.ToObject<ProductsResponse>();

                if (response?.Products?.Nodes == null)
                {
                    return products;
                }

                products.AddRange(
                    response.Products.Nodes);

                cursor =
                    response.Products.PageInfo?.EndCursor;

                hasNextPage =
                    response.Products.PageInfo?.HasNextPage == true;

            }
            while (hasNextPage);

            return products;
        }

        public async Task<List<Product>> GetProductsListAsync(string graphQLFilter = null)
        {
            var graphQlProducts = await GetGraphQlProductsAsync(graphQLFilter);

            var products = graphQlProducts.Select(ShopifyGraphQlHelper.MapProduct).ToList();

            return products;
        }
    }
}
