using Microsoft.AspNetCore.DataProtection;
using ShopifySharp.Filters;
using ShopifySharp;
using SyncApp.Models.EF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncApp.Logic
{
    public class CountriesLogic
    {
        private readonly ShopifyAppContext _context;
        public CountriesLogic(ShopifyAppContext context)
        {
            _context = context;
        }

        private Configrations Config
        {
            get
            {
                return _context.Configrations.First();
            }
        }

        private string StoreUrl
        {
            get
            {
                return Config.StoreUrl ?? string.Empty;
            }
        }
        private string ApiSecret
        {
            get
            {
                return Config.ApiSecret ?? string.Empty;
            }
        }
        public Countries GetCountry(long countryId)
        {
            if (countryId != 0)
            {
                var country = _context.Countries.FirstOrDefault(w => w.CountryId == countryId);
                return country;
            }

            return null;
        }
        
        public Countries GetCountryByName(string country)
        {
            if (!string.IsNullOrEmpty(country))
            {
                var countryDB = _context.Countries.FirstOrDefault(w => w.CountryName == country);
                return countryDB;
            }

            return null;
        }

        public List<Countries> GetWarehouses()
        {
            return _context.Countries.ToList();
        }

        public Countries GetDefaultCountry()
        {
            var country = _context.Countries.FirstOrDefault(w => w.IsDefault);
            return country;
        }

        internal bool CheckIfHasValues(Countries country)
        {
            return !string.IsNullOrEmpty(country?.BranchCode) && !string.IsNullOrEmpty(country?.CustomerCode) && country?.CountryTax != null;
        }

        public async Task<List<Country>> GetCountriesAsync()
        {
            List<Country> countries = new List<Country>();

            var countryService = new CountryService(StoreUrl, ApiSecret);

            var page = await countryService.ListAsync(new CountryListFilter { Limit = 250 });

            while (true)
            {
                countries.AddRange(page.Items);

                if (!page.HasNextPage)
                {
                    break;
                }

                try
                {
                    page = await countryService.ListAsync(page.GetNextPageFilter());
                }
                catch (ShopifyRateLimitException)
                {
                    await Task.Delay(10000);

                    page = await countryService.ListAsync(page.GetNextPageFilter());
                }
            }

            return countries;
        }
    }
}