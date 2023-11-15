using Microsoft.AspNetCore.DataProtection;
using ShopifySharp.Filters;
using ShopifySharp;
using SyncApp.Models.EF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

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
        public async Task<Countries> GetCountry(long countryId)
        {
            if (countryId != 0)
            {
                var country = await _context.Countries.FirstOrDefaultAsync(w => w.CountryId == countryId);
                return country;
            }

            return null;
        }

        public async Task<Countries> GetCountryByName(string country)
        {
            if (!string.IsNullOrEmpty(country))
            {
                var countryDB = await _context.Countries.FirstOrDefaultAsync(w => w.CountryName == country);
                return countryDB;
            }

            return null;
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

        public async Task<List<Country>> GetShopifyCountries()
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

        public async Task UpdateOrAddCountries()
        {
            var shopifyCountries = await GetShopifyCountries();

            foreach (var country in shopifyCountries)
            {
                var countryDB = await GetCountryByName(country.Name);
                if (countryDB != null)
                {
                    if (Config.GetTaxFromShopify.GetValueOrDefault())
                    {
                        countryDB.CountryId = country.Id;
                        countryDB.CountryTax = country.Tax;
                        _context.Update(countryDB);
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    Countries countryModel = new Countries
                    {
                        Id = 0,
                        CountryId = country.Id,
                        CountryName = country.Name,
                        CountryCode = country.Code,
                    };
                    if (Config.GetTaxFromShopify.GetValueOrDefault())
                    {
                        countryModel.CountryTax = country.Tax;
                    }
                    await _context.AddAsync(countryModel);
                }
            }

            await _context.SaveChangesAsync();

        }
    }
}