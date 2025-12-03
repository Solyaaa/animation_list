using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoListApp.Domain.Entities;
using TodoListApp.Infrastructure.Persistence;
using TodoListApp.WebApi.Models.Tasks;
using TaskStatus = TodoListApp.Domain.Entities.TaskStatus;

namespace TodoListApp.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly AppDbContext _db;
    public TasksController(AppDbContext db) => _db = db;

    private string UserId =>
        User.FindFirstValue("uid") ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    [HttpPost]
    public async Task<ActionResult<int>> Create([FromBody] CreateTaskDto r)
    {
        var list = await _db.TodoLists
            .FirstOrDefaultAsync(x => x.Id == r.ListId && x.OwnerId == UserId);
        if (list is null) return NotFound("List not found");

        var t = new TodoTask
        {
            Title = r.Title,
            Description = r.Description,
            DueDate = r.DueDate,
            TodoListId = r.ListId,
            AssignedUserId = UserId,
            Status = TaskStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.TodoTasks.Add(t);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = t.Id }, t.Id);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TaskItemDto>> GetById(int id)
    {
        var t = await _db.TodoTasks
            .Include(x => x.TaskTags).ThenInclude(tt => tt.Tag)
            .Include(x => x.TodoList)
            .FirstOrDefaultAsync(x => x.Id == id &&
                                      (x.TodoList!.OwnerId == UserId || x.AssignedUserId == UserId));
        if (t is null) return NotFound();

        return new TaskItemDto
        {
            Id = t.Id,
            TodoListId = t.TodoListId,
            Title = t.Title,
            Description = t.Description,
            DueDate = t.DueDate,
            Status = t.Status.ToString(), // ← КОНВЕРТУЄМО В STRING
            AssignedUserId = t.AssignedUserId,
            Tags = t.TaskTags
                .Select(tt => new WebApi.Models.Tags.TagDto { Id = tt.TagId, Name = tt.Tag!.Name })
                .ToList()
        };
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTaskDto dto)
    {
        var t = await _db.TodoTasks
            .Include(x => x.TodoList)
            .FirstOrDefaultAsync(x => x.Id == id &&
                                      (x.TodoList!.OwnerId == UserId || x.AssignedUserId == UserId));
        if (t is null) return NotFound();

        t.Title = dto.Title;
        t.Description = dto.Description;
        t.DueDate = dto.DueDate;

        // КОНВЕРТУЄМО string → enum
        if (Enum.TryParse<TaskStatus>(dto.Status, true, out var status))
        {
            t.Status = status;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var t = await _db.TodoTasks.Include(x => x.TodoList)
            .FirstOrDefaultAsync(x => x.Id == id && x.TodoList!.OwnerId == UserId);
        if (t is null) return NotFound();

        _db.TodoTasks.Remove(t);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("my-assigned")]
    public async Task<IEnumerable<TaskItemDto>> MyAssigned([FromQuery] string? status = null, [FromQuery] string? sortBy = null)
    {
        var q = _db.TodoTasks
            .Include(t => t.TodoList)
            .Include(t => t.TaskTags).ThenInclude(tt => tt.Tag)
            .Where(t => t.AssignedUserId == UserId);

        // Фільтрація за статусом (string → enum)
        if (!string.IsNullOrEmpty(status))
        {
            if (Enum.TryParse<TaskStatus>(status, true, out var statusEnum))
            {
                q = q.Where(t => t.Status == statusEnum);
            }
        }
        else
        {
            q = q.Where(t => t.Status != TaskStatus.Done);
        }

        q = (sortBy?.ToLowerInvariant()) switch
        {
            "name" or "title" => q.OrderBy(t => t.Title),
            "due" or "duedate" => q.OrderBy(t => t.DueDate),
            _ => q.OrderBy(t => t.CreatedAt)
        };

        return await q.Select(t => new TaskItemDto
        {
            Id = t.Id,
            TodoListId = t.TodoListId,
            Title = t.Title,
            Description = t.Description,
            DueDate = t.DueDate,
            Status = t.Status.ToString(), // ← КОНВЕРТУЄМО В STRING
            AssignedUserId = t.AssignedUserId,
            Tags = t.TaskTags
                .Select(tt => new WebApi.Models.Tags.TagDto { Id = tt.TagId, Name = tt.Tag!.Name })
                .ToList()
        }).ToListAsync();
    }

    [HttpPatch("{id:int}/status/{status}")]
    public async Task<IActionResult> ChangeStatus(int id, TaskStatus status)
    {
        var t = await _db.TodoTasks.Include(x => x.TodoList)
            .FirstOrDefaultAsync(x => x.Id == id &&
                                      (x.AssignedUserId == UserId || x.TodoList!.OwnerId == UserId));
        if (t is null) return NotFound();

        t.Status = status;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("search")]
    public async Task<IEnumerable<TaskItemDto>> Search(string? title, DateTime? createdFrom, DateTime? dueTo)
    {
        var q = _db.TodoTasks
            .Include(t => t.TodoList)
            .Include(t => t.TaskTags).ThenInclude(tt => tt.Tag)
            .Where(t => t.TodoList!.OwnerId == UserId || t.AssignedUserId == UserId);

        if (!string.IsNullOrWhiteSpace(title)) q = q.Where(t => t.Title.Contains(title));
        if (createdFrom.HasValue) q = q.Where(t => t.CreatedAt >= createdFrom.Value);
        if (dueTo.HasValue) q = q.Where(t => t.DueDate != null && t.DueDate <= dueTo.Value);

        return await q.Select(t => new TaskItemDto
        {
            Id = t.Id,
            TodoListId = t.TodoListId,
            Title = t.Title,
            Description = t.Description,
            DueDate = t.DueDate,
            Status = t.Status.ToString(), // ← КОНВЕРТУЄМО В STRING
            AssignedUserId = t.AssignedUserId,
            Tags = t.TaskTags
                .Select(tt => new WebApi.Models.Tags.TagDto { Id = tt.TagId, Name = tt.Tag!.Name })
                .ToList()
        }).ToListAsync();
    }
}