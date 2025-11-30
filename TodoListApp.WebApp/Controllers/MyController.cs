using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoListApp.WebApp.Services;

namespace TodoListApp.WebApp.Controllers;

[Authorize]
public class MyController(IApiClient api) : Controller
{
    public async Task<IActionResult> Assigned(int? status, string? sortBy)
        => View(await api.GetAssignedAsync(status, sortBy));

    public async Task<IActionResult> Search(string? title, DateTime? createdFrom, DateTime? dueTo)
        => View(await api.SearchAsync(title, createdFrom, dueTo));
}