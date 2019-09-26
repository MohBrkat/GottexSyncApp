using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SyncApp.Filters;
using SyncApp.Models.EF;

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

        // POST: Configrations/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        

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
                    if(!configrations.UseRecurringJob.GetValueOrDefault())
                    {
                        _context.Database.ExecuteSqlCommand("DELETE FROM [Hangfire].[Hash];DELETE FROM [Hangfire].[JOB];DELETE FROM [Hangfire].[Set]");

                    }
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                     
                        throw;
                     
                }
                return RedirectToAction("Index","Home");
            }
            return View(configrations);
        }





    }
}
