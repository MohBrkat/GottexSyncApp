﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SyncApp.Filters;
using SyncApp.Models.EF;
using SyncApp.ViewModel;
using SyncApp.Models.Enums;
using SyncApp.Logic;
using ShopifySharp;

namespace SyncApp.Controllers
{
    [Auth]
    public class ConfigrationsController : Controller
    {
        private readonly ShopifyAppContext _context;

        public ConfigrationsController(ShopifyAppContext context)
        {
            _context = context;
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
        // POST: Configrations/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.


        // GET: Configrations/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            ConfigurationsModel configs = new ConfigurationsModel();

            if (id == null)
            {
                return NotFound();
            }

            var configrations = await _context.Configrations.FindAsync(id);
            if (configrations == null)
            {
                return NotFound();
            }

            configs.Configurations = configrations;

            await new ReportsScheduleLogic(_context, (int)ReportTypesEnum.DailyReport).GetScheduleReports(configs);

            return View(configs);
        }

        // POST: Configrations/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        public async Task<IActionResult> Edit(ConfigurationsModel configs)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(configs.Configurations);
                    if (!configs.Configurations.UseRecurringJob.GetValueOrDefault())
                    {
                        _context.Database.ExecuteSqlCommand("DELETE FROM [Hangfire].[Hash];DELETE FROM [Hangfire].[JOB];DELETE FROM [Hangfire].[Set]");
                    }

                    await new ReportsScheduleLogic(_context, (int)ReportTypesEnum.DailyReport).UpdateScheduleReportsAsync(configs);

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {

                    throw;

                }

                return RedirectToAction("Index", "Home");
            }
            else
            {
                var message = string.Join(" | ", ModelState.Values
                                            .SelectMany(v => v.Errors)
                                            .Select(e => e.ErrorMessage));
            }


            return View(configs);
        }


        public async Task<IActionResult> Warehouses()
        {
            WarehouseModel configs = new WarehouseModel
            {
                Warehouses = new List<Warehouse>()

            };

            var locationService = new LocationService(StoreUrl, ApiSecret);
            var shopifyLocations = await locationService.ListAsync();

            foreach (var loc in shopifyLocations)
            {
                Warehouse warehouse = new Warehouse
                {
                    Id = 1,
                    WarehouseId = loc.Id.ToString(),
                    WarehouseName = loc.Name,
                    WarehouseCode = "ON01",
                    IsDefault = true
                };

                configs.Warehouses.Add(warehouse);
            }

            configs.Warehouses.Add(new Warehouse
            {
                Id = 2,
                WarehouseId = "ttt",
                WarehouseName = "test",
                WarehouseCode = "test",
                IsDefault = false
            });


            configs.Warehouses.Add(new Warehouse
            {
                Id = 2,
                WarehouseId = "ttt",
                WarehouseName = "test",
                WarehouseCode = "test",
                IsDefault = false
            });


            configs.Warehouses.Add(new Warehouse
            {
                Id = 2,
                WarehouseId = "ttt",
                WarehouseName = "test",
                WarehouseCode = "test",
                IsDefault = false
            });

            return View(configs);
        }
    }
}
