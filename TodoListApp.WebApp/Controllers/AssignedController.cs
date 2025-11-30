using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoListApp.WebApp.Services;

namespace TodoListApp.WebApp.Controllers;

[Authorize]
public class AssignedController(IApiClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? status, string? sortBy)
    {
        var tasks = await api.GetAssignedAsync(status, sortBy);
        ViewBag.Status = status;
        ViewBag.SortBy = sortBy;
        return View(tasks);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(int taskId, int status, int? filterStatus, string? sortBy)
    {
        await api.ChangeStatusAsync(taskId, status);
        return RedirectToAction(nameof(Index), new { status = filterStatus, sortBy });
    }
}