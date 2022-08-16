using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace SyncAppEntities.Filters
{
    public class Auth : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var sessionValue = filterContext.HttpContext.Session.GetString("User");
            if (!string.IsNullOrEmpty(sessionValue))
            {
                base.OnActionExecuting(filterContext);
                return;
            }
            else
            {
                filterContext.Result = new RedirectToRouteResult(
    new RouteValueDictionary(new { controller = "Account", action = "Login" })
);

                filterContext.Result.ExecuteResultAsync(filterContext);


            }

        }

        //    protected override void OnActionExecuting(ActionExecutingContext filterContext)
        //    {
        //        string actionName = filterContext.ActionDescriptor.ActionName;
        //        string controllerName = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName
        //        .....
        //base.OnActionExecuting(filterContext);
        //    }

    }
}