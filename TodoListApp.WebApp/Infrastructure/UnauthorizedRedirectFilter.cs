using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TodoListApp.WebApp.Infrastructure;

public sealed class UnauthorizedRedirectFilter : IAsyncExceptionFilter
{
    public Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is UnauthorizedAccessException)
        {
            context.ExceptionHandled = true;
            context.Result = new RedirectToActionResult("Login", "Auth", null);
        }
        return Task.CompletedTask;
    }
}