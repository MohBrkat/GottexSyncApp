using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SyncAppEntities.Filters;
using SyncAppEntities.Models.EF;

namespace SyncAppEntities.Controllers
{
    public class AccountController : Controller
    {
        private readonly ShopifyAppContext _context;

        public AccountController(ShopifyAppContext context)
        {
            _context = context;
        }
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string userName, string password)
        {
            var user = _context.Users.SingleOrDefault(a => a.UserName == userName && a.Password == password);
            if (user != null)
            {
                HttpContext.Session.SetString("User", user.UserName);
                return RedirectToAction("Index", "Home");
            }
            else
            {
                return RedirectToAction("login", "Account");
            }
        }
        [Auth]
        public ActionResult ChangePassword()
        {
            return View();
        }

        [Auth]
        [HttpPost]
        public ActionResult ChangePassword(string currentPassword, string newPassword)
        {
            var session = HttpContext.Session.GetString("User");
            if (session != null)
            {
                var user = _context.Users.SingleOrDefault(a => a.Password == currentPassword && a.UserName == session);
                if (user != null)
                {
                    if (!string.IsNullOrEmpty(newPassword) && newPassword.Length > 0)
                    {
                        user.Password = newPassword;
                        _context.SaveChanges();
                        return RedirectToAction("Index", "Home");
                    }
                    else
                    {
                        return RedirectToAction("ChangePassword", "Account", new { msg = "password cannot be empty" });

                    }
                }
                else
                {
                    return RedirectToAction("ChangePassword", "Account", new { msg = "old password not correct." });

                }
            }
            return null;
        }
    }
}