using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Student_Information_system.Models;
using Student_Information_system.Services;

namespace Student_Information_system.Controllers
{
    public abstract class SisControllerBase : Controller
    {
        protected readonly DataStore Store;
        protected SisControllerBase(DataStore store) => Store = store;

        protected string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
        protected AppUser? Me => Store.User(CurrentUserId);

        protected void Flash(string message, string kind = "success")
        {
            TempData["Flash"] = message;
            TempData["FlashKind"] = kind;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            ViewBag.Store = Store;
            var me = Me;
            ViewBag.Me = me;
            if (me != null)
                ViewBag.UnreadCount = Store.MessagesFor(me).Count(m => m.SenderId != me.Id && !m.ReadBy.Contains(me.Id) && me.Role != Roles.Management);
            base.OnActionExecuting(context);
        }
    }
}
