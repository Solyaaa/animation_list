using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoListApp.Infrastructure.Persistence;
using TodoListApp.WebApi.Models.Tasks;
using TodoListApp.Domain.Entities;
using TaskStatus = TodoListApp.Domain.Entities.TaskStatus;

namespace TodoListApp.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AssignedController : ControllerBase
{
    private readonly AppDbContext db;
    public AssignedController(AppDbContext db) => this.db = db;

    private string UserId => User.FindFirst("uid")?.Value ?? "";

    [HttpGet("my")]
    public async Task<IEnumerable<TaskItemDto>> MyAssigned([FromQuery] string? status = null, [FromQuery] string? sortBy = null)
    {
        var q = db.TodoTasks
            .Include(t => t.TodoList)
            .Include(t => t.TaskTags).ThenInclude(x => x.Tag)
            .Where(t => t.AssignedUserId == UserId);

        // Фільтрація за статусом
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
            _ => q.OrderBy(t => t.Id)
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
}