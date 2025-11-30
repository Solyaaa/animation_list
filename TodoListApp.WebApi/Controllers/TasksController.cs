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

    // POST api/tasks  (WebApp шле сюди тіло з ListId, Title, ...)
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
            AssignedUserId = UserId,          // US07: призначаємо автора
            Status = TaskStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.TodoTasks.Add(t);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = t.Id }, t.Id);
    }

    // (НЕобов’язково) альтернативний вкладений маршрут: POST api/lists/{listId}/tasks
    [HttpPost("~/api/lists/{listId:int}/tasks")]
    public async Task<ActionResult<int>> CreateInList(int listId, [FromBody] CreateTaskDto r)
    {
        r.ListId = listId;
        return await Create(r);
    }

    // GET api/tasks/{id}
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
            Status = t.Status,
            AssignedUserId = t.AssignedUserId,
            Tags = t.TaskTags
                .Select(tt => new WebApi.Models.Tags.TagDto { Id = tt.TagId, Name = tt.Tag!.Name })
                .ToList()
        };
    }

    // PUT api/tasks/{id}
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
        t.Status = dto.Status;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE api/tasks/{id}
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

    // Assigned (US11–US14)
    [HttpGet("my-assigned")]
    public async Task<IEnumerable<TaskItemDto>> MyAssigned([FromQuery] TaskStatus? status, [FromQuery] string? sortBy)
    {
        var q = _db.TodoTasks.Include(t => t.TodoList)
            .Where(t => t.AssignedUserId == UserId);

        q = status.HasValue ? q.Where(t => t.Status == status)
                            : q.Where(t => t.Status != TaskStatus.Done);

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
            Status = t.Status,
            AssignedUserId = t.AssignedUserId
        }).ToListAsync();
    }

    // PATCH api/tasks/{id}/status/{status}
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

    // Search (US15, US16)
    [HttpGet("search")]
    public async Task<IEnumerable<TaskItemDto>> Search(string? title, DateTime? createdFrom, DateTime? dueTo)
    {
        var q = _db.TodoTasks.Include(t => t.TodoList)
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
            Status = t.Status,
            AssignedUserId = t.AssignedUserId
        }).ToListAsync();
    }
}
