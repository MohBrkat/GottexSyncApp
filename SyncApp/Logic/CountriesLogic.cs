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

        internal bool CheckIfHasValues(string countryName)
        {
            var country = GetCountryByName(countryName);
            return !string.IsNullOrEmpty(country?.BranchCode) && !string.IsNullOrEmpty(country?.CustomerCode) && country?.CountryTax != null;
        }
    }
}