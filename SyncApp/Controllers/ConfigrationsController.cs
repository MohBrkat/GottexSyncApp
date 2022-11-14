﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SyncAppEntities.Filters;
using SyncAppEntities.Models.EF;
using SyncAppEntities.ViewModel;
using SyncAppEntities.Models.Enums;
using SyncAppEntities.Logic;
using ShopifySharp;

namespace SyncAppEntities.Controllers
{
    [Auth]
    public class ConfigrationsController : Controller
    {
        private readonly ShopifyAppContext _context;
        private readonly WarehouseLogic _warehouseLogic;

        public ConfigrationsController(ShopifyAppContext context)
        {
            _context = context;
            _warehouseLogic = new WarehouseLogic(context);
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
                        _context.Database.ExecuteSqlRaw("DELETE FROM [Hangfire].[Hash];DELETE FROM [Hangfire].[JOB];DELETE FROM [Hangfire].[Set]");
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
            WarehouseModel warehouseModel = new WarehouseModel
            {
                WarehousesList = new List<Warehouses>()
            };

            var locationService = new LocationService(StoreUrl, ApiSecret);
            var shopifyLocations = await locationService.ListAsync();

            DeleteNotExistingLocations(shopifyLocations);

            foreach (var loc in shopifyLocations)
            {
                var warehouseDB = _warehouseLogic.GetWarehouse(loc.Id ?? 0);
                Warehouses warehouse = new Warehouses
                {
                    Id = warehouseDB?.Id ?? 0,
                    WarehouseId = loc.Id,
                    WarehouseName = loc.Name,
                    WarehouseCode = warehouseDB?.WarehouseCode,
                    IsDefault = warehouseDB?.IsDefault ?? false
                };

                warehouseModel.WarehousesList.Add(warehouse);
            }

            return View(warehouseModel);
        }

        private void DeleteNotExistingLocations(IEnumerable<Location> shopifyLocations)
        {
            var locationsIds = shopifyLocations.Select(a => a.Id).ToList();
            var DBLocations = _warehouseLogic.GetWarehouses();

            var dataToDelete = DBLocations.Where(a => !locationsIds.Contains(a.WarehouseId)).ToList();
            _warehouseLogic.DeleteWarehouses(dataToDelete);
        }

        [HttpPost]
        public async Task<IActionResult> Warehouses(WarehouseModel warehouse)
        {
            string message = string.Empty;
            if (ModelState.IsValid)
            {
                try
                {
                    foreach (var whouse in warehouse.WarehousesList)
                    {
                        var warehouseDB = _warehouseLogic.GetWarehouse(whouse.WarehouseId ?? 0);
                        if (warehouseDB != null)
                        {
                            warehouseDB.WarehouseCode = whouse.WarehouseCode;
                            warehouseDB.WarehouseName = whouse.WarehouseName;
                            warehouseDB.IsDefault = whouse.IsDefault;
                            _context.Update(warehouseDB);
                        }
                        else
                        {
                            _context.Add(whouse);
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

            warehouse.Message = message;

            return View(warehouse);
        }
    }
}
