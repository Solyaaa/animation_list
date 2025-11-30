using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoListApp.WebApp.Models;
using TodoListApp.WebApp.Services;

namespace TodoListApp.WebApp.Controllers;

[Authorize] // ✅ без логіну — не пускаємо
public class ListsController : Controller
{
    private readonly IApiClient api;
    public ListsController(IApiClient api) => this.api = api;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // ✅ якщо токен зник (напр., сесія перезʼїхала) — редірект замість 401
        if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("access_token")))
            return RedirectToAction("Login", "Auth");

        try
        {
            var lists = await api.GetListsAsync();
            return View(lists);
        }
        catch (UnauthorizedAccessException)
        {
            // токен прострочився/некоректний — перелогін
            return RedirectToAction("Login", "Auth");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var lists = await api.GetListsAsync();
            var item = lists.FirstOrDefault(x => x.Id == id);
            if (item is null) return NotFound();
            return View(item);
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login", "Auth");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string title, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            ModelState.AddModelError("", "Title is required.");
            return await Edit(id);
        }

        try
        {
            await api.UpdateListAsync(id, new UpdateListDto
            {
                Title = title.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim()
            });
            return RedirectToAction(nameof(Index));
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login", "Auth");
        }
    }

    [HttpGet]
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string title, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            ModelState.AddModelError("", "Title is required.");
            return View();
        }

        try
        {
            await api.CreateListAsync(new CreateListDto
            {
                Title = title.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim()
            });
            return RedirectToAction(nameof(Index));
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login", "Auth");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await api.DeleteListAsync(id);
            return RedirectToAction(nameof(Index));
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login", "Auth");
        }
    }

    // ===== Tasks in List =====
    [HttpGet]
    public async Task<IActionResult> Tasks(int id)
    {
        try
        {
            var items = await api.GetTasksInListAsync(id); // [] якщо нема
            ViewBag.ListId = id;
            return View(items);
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login", "Auth");
        }
    }

    // TodoListApp.WebApp/Controllers/ListsController.cs
    [HttpPost]
    public async Task<IActionResult> CreateTask(int listId, string title, string? description, DateTime? dueDate)
    {
        await api.CreateTaskAsync(listId, new CreateTaskDto
        {
            Title = title,
            Description = description,
            DueDate = dueDate
        });
        return RedirectToAction(nameof(Tasks), new { id = listId });
    }


    [HttpGet, ActionName("CreateTask")]
    public IActionResult CreateTaskGet(int? listId)
        => listId is int id ? RedirectToAction(nameof(Tasks), new { id }) : RedirectToAction(nameof(Index));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(int listId, int taskId, int status)
    {
        try
        {
            await api.ChangeStatusAsync(taskId, status);
            return RedirectToAction(nameof(Tasks), new { id = listId });
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login", "Auth");
        }
    }

    [HttpGet, ActionName("SetStatus")]
    public IActionResult SetStatusGet(int listId, int taskId, int status)
        => RedirectToAction(nameof(Tasks), new { id = listId });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTag(int listId, int taskId, string name)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(name))
                await api.AddTagAsync(taskId, name.Trim());
            return RedirectToAction(nameof(Tasks), new { id = listId });
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login", "Auth");
        }
    }

    [HttpGet, ActionName("AddTag")]
    public IActionResult AddTagGet(int listId, int taskId, string? name)
        => RedirectToAction(nameof(Tasks), new { id = listId });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveTag(int listId, int taskId, int tagId)
    {
        try
        {
            await api.RemoveTagAsync(taskId, tagId);
            return RedirectToAction(nameof(Tasks), new { id = listId });
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login", "Auth");
        }
    }

    [HttpGet, ActionName("RemoveTag")]
    public IActionResult RemoveTagGet(int listId, int taskId, int tagId)
        => RedirectToAction(nameof(Tasks), new { id = listId });
}

