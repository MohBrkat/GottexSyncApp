using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using ShopifySharp;
using ShopifySharp.Filters;
using SyncApp.Filters;
using SyncApp.Logic;
using SyncApp.Models.EF;
using SyncApp.ViewModel;
using Log4NetLibrary;

namespace SyncApp.Controllers
{
    [Auth]
    public class ConfigrationsController : Controller
    {
        private readonly ShopifyAppContext _context;
        private readonly CountriesLogic _countriesLogic;
        private static readonly log4net.ILog _log = Logger.GetLogger();
        public ConfigrationsController(ShopifyAppContext context)
        {
            _context = context;
            _countriesLogic = new CountriesLogic(context);
        }

        #region fields
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
        #endregion

        // GET: Configrations/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var configrations = await _context.Configrations.FindAsync(id);
            if (configrations == null)
            {
                return NotFound();
            }
            return View(configrations);
        }

        // POST: Configrations/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        public async Task<IActionResult> Edit(Configrations configrations)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(configrations);
                    if (!configrations.UseRecurringJob.GetValueOrDefault())
                    {
                        _context.Database.ExecuteSqlCommand("DELETE FROM [Hangfire].[Hash];DELETE FROM [Hangfire].[JOB];DELETE FROM [Hangfire].[Set]");

                    }
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {

                    throw;

                }
                return RedirectToAction("Index", "Home");
            }
            return View(configrations);
        }

        public async Task<decimal?> GetTaxFromShopify(long id)
        {
            try
            {
                var countryService = new CountryService(StoreUrl, ApiSecret);
                Country country = await countryService.GetAsync(id);
                var tax = country.Tax ?? 0;
                var countryDB = _countriesLogic.GetCountry(id);
                if (countryDB != null)
                {
                    countryDB.CountryTax = tax;
                    _context.Update(countryDB);
                    await _context.SaveChangesAsync();
                }
                return Math.Round(tax, 2);
            }
            catch (Exception ex)
            {
                _log.Error($"Exception While Getting Taxes From Shopify: {ex.Message}", ex);
                throw ex;
            }
        }

        public async Task<IActionResult> Countries()
        {
            CountriesModel countriesModel = new CountriesModel
            {
                CountriesList = new List<Countries>()
            };

            var shopifyCountries = await _countriesLogic.GetShopifyCountries();

            foreach (var country in shopifyCountries)
            {
                var countryDB = _countriesLogic.GetCountry(country.Id ?? 0);
                Countries countryModel = new Countries
                {
                    Id = countryDB?.Id ?? 0,
                    CountryId = country.Id,
                    CountryName = country.Name,
                    CountryCode = country.Code,
                    CountryTax = countryDB?.CountryTax ?? 0m,
                    BranchCode = countryDB?.BranchCode,
                    CustomerCode = countryDB?.CustomerCode,
                    IsDefault = countryDB?.IsDefault ?? false
                };

                countriesModel.CountriesList.Add(countryModel);
            }

            return View(countriesModel);
        }

        [HttpPost]
        public async Task<IActionResult> Countries(CountriesModel countries)
        {
            string message = string.Empty;
            if (ModelState.IsValid)
            {
                try
                {
                    foreach (var country in countries.CountriesList)
                    {
                        var warehouseDB = _countriesLogic.GetCountry(country.CountryId ?? 0);
                        if (warehouseDB != null)
                        {
                            warehouseDB.CountryTax = country.CountryTax ?? 0m;
                            warehouseDB.BranchCode = country.BranchCode;
                            warehouseDB.CustomerCode = country.CustomerCode;
                            warehouseDB.IsDefault = country.IsDefault;
                            _context.Update(warehouseDB);
                        }
                        else
                        {
                            _context.Add(country);
                        }
                    }

                    await _context.SaveChangesAsync();

                    message = $"Changes Saved Successfully";
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    throw ex;
                }
            }
            else
            {
                message = string.Join(" | ", ModelState.Values
                                            .SelectMany(v => v.Errors)
                                            .Select(e => e.ErrorMessage));
            }

            countries.Message = message;

            return View(countries);
        }
    }
}
