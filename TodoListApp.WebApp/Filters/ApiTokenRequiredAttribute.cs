using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TodoListApp.WebApp.Filters;

public class ApiTokenRequiredAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var jwt = context.HttpContext.Session.GetString("JWT");
        if (string.IsNullOrWhiteSpace(jwt))
        {
            context.Result = new RedirectToActionResult("Login", "Account", null);
            return;
        }
        base.OnActionExecuting(context);
    }
}