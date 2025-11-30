using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoListApp.WebApi.Models.Tasks;
using TodoListApp.WebApp.Models;
using TodoListApp.WebApp.Services;
using UpdateTaskDto = TodoListApp.WebApp.Models.UpdateTaskDto;

namespace TodoListApp.WebApp.Controllers;

[Authorize]
public class TasksController(IApiClient api) : Controller
{
    // Деталі задачі + коментарі (US06 + US22)
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var t = await api.GetTaskAsync(id);
        if (t is null) return RedirectToAction("Index", "Lists");
        ViewBag.Comments = await api.GetCommentsAsync(id);
        return View(t);
    }

    // Редагування задачі (US09)
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var t = await api.GetTaskAsync(id);
        if (t is null) return RedirectToAction("Index", "Lists");
        var vm = new UpdateTaskDto
        {
            Title = t.Title,
            Description = t.Description,
            DueDate = t.DueDate,
            Status = t.Status,
            AssignedUserId = t.AssignedUserId
        };
        ViewBag.TaskId = id;
        ViewBag.ListId = t.TodoListId;
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UpdateTaskDto vm)
    {
        await api.UpdateTaskAsync(id, vm);
        return RedirectToAction(nameof(Details), new { id });
    }

    // Коментарі (US23–US25)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int taskId, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            TempData["Error"] = "Comment text is required.";
            return RedirectToAction(nameof(Details), new { id = taskId });
        }

        await api.AddCommentAsync(taskId, text);
        return RedirectToAction(nameof(Details), new { id = taskId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditComment(int taskId, int commentId, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            TempData["Error"] = "Comment text is required.";
            return RedirectToAction(nameof(Details), new { id = taskId });
        }

        await api.EditCommentAsync(taskId, commentId, text);
        return RedirectToAction(nameof(Details), new { id = taskId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(int taskId, int commentId)
    {
        await api.DeleteCommentAsync(taskId, commentId);
        return RedirectToAction(nameof(Details), new { id = taskId });
    }
}
