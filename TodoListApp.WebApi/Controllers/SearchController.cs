using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoListApp.Infrastructure.Persistence;
using TodoListApp.WebApi.Models.Tasks;
using TodoListApp.WebApi.Models.Tags;
using WebApiTagDto = TodoListApp.WebApi.Models.Tags.TagDto;

namespace TodoListApp.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class SearchController(AppDbContext db) : ControllerBase
{
    // GET /api/search?title=&createdFrom=&dueTo=
    [HttpGet("search")]
    public async Task<IEnumerable<TaskItemDto>> Search(
        [FromQuery] string? title,
        [FromQuery] DateTime? createdFrom,
        [FromQuery] DateTime? dueTo)
    {
        var q = db.TodoTasks
            .Include(t => t.TaskTags).ThenInclude(tt => tt.Tag)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(title))
            q = q.Where(t => t.Title.Contains(title));

        if (createdFrom is not null)
            q = q.Where(t => t.CreatedAt >= createdFrom);

        if (dueTo is not null)
            q = q.Where(t => t.DueDate != null && t.DueDate <= dueTo);

        return await q
            .OrderBy(t => t.Id)
            .Select(t => new TaskItemDto
            {
                Id = t.Id,
                TodoListId = t.TodoListId,
                Title = t.Title,
                Description = t.Description,
                DueDate = t.DueDate,
                Status = t.Status,
                AssignedUserId = t.AssignedUserId,
                Tags = t.TaskTags.Select(tt => new WebApiTagDto
                {
                    Id = tt.TagId, Name = tt.Tag!.Name
                }).ToList()
            })
            .ToListAsync();
    }
}
