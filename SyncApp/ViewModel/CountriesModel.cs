using SyncApp.Models.EF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncApp.ViewModel
{
    public class CountriesModel
    {
        public List<Countries> CountriesList { get; set; }
        public string Message { get; set; }
    }

    public class CountryModel
    {
        public int Id { get; set; }
        public long? CountryId { get; set; }
        public string CountryName { get; set; }
        public string CountryCode { get; set; }
        public decimal CountryTax { get; set; }
        public string BranchCode { get; set; }
        public string CustomerCode { get; set; }
        public bool IsDefault { get; set; }
    }
}
