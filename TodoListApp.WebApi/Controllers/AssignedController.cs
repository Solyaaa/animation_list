using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoListApp.Infrastructure.Persistence;
using TodoListApp.WebApi.Models.Tasks;
using TodoListApp.WebApi.Models.Tags;
using TodoListApp.Domain.Entities;
using TaskStatus = TodoListApp.Domain.Entities.TaskStatus;
// Alias, щоб уникнути конфлікту з іншими TagDto
using WebApiTagDto = TodoListApp.WebApi.Models.Tags.TagDto;

namespace TodoListApp.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AssignedController : ControllerBase
{
    private readonly AppDbContext db;
    public AssignedController(AppDbContext db) => this.db = db;

    private string UserId => User.FindFirst("uid")?.Value ?? "";

    // GET /api/assigned?status=&sortBy=
    [HttpGet("my")]
    public async Task<IEnumerable<TaskItemDto>> MyAssigned([FromQuery] int? status, [FromQuery] string? sortBy)
    {
        var q = db.TodoTasks
            .Include(t => t.TodoList)
            .Include(t => t.TaskTags).ThenInclude(x => x.Tag)
            .Where(t => t.AssignedUserId == UserId);

        // US12: за замовчуванням лише активні (не Done)
        if (status is null)
            q = q.Where(t => t.Status != TaskStatus.Done);
        else
            q = q.Where(t => (int)t.Status == status);

        // US13: сортування
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
            Status = t.Status,
            AssignedUserId = t.AssignedUserId,
            Tags = t.TaskTags
                .Select(tt => new WebApiTagDto { Id = tt.TagId, Name = tt.Tag!.Name })
                .ToList()
        }).ToListAsync();
    }
}
