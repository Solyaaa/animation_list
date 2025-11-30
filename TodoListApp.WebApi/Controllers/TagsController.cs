using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoListApp.Domain.Entities;
using TodoListApp.Infrastructure.Persistence;
using TodoListApp.WebApi.Models.Tasks;
using TodoListApp.WebApi.Models.Tags;

using WebApiTagDto = TodoListApp.WebApi.Models.Tags.TagDto;

namespace TodoListApp.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class TagsController(AppDbContext db) : ControllerBase
{
    private string UserId => User.FindFirst("uid")?.Value ?? "";

    // US17: теги задачі
    [HttpGet("tasks/{taskId:int}/tags")]
    public async Task<IEnumerable<WebApiTagDto>> GetTaskTags(int taskId)
    {
        var ok = await db.TodoTasks.Include(t => t.TodoList)
            .AnyAsync(t => t.Id == taskId && (t.TodoList!.OwnerId == UserId || t.AssignedUserId == UserId));
        if (!ok) return Enumerable.Empty<WebApiTagDto>();

        return await db.TaskTags
            .Where(tt => tt.TodoTaskId == taskId)
            .Select(tt => new WebApiTagDto { Id = tt.TagId, Name = tt.Tag!.Name })
            .ToListAsync();
    }

    // US20: додати тег до задачі (створює тег, якщо його ще немає)
    [HttpPost("tasks/{taskId:int}/tags")]
    public async Task<IActionResult> AddTagToTask(int taskId, [FromBody] AddTagDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest("Tag name is required.");

        var task = await db.TodoTasks.Include(t => t.TodoList)
            .FirstOrDefaultAsync(t => t.Id == taskId && t.TodoList!.OwnerId == UserId);
        if (task is null) return NotFound("Task not found or no permission.");

        var tagName = dto.Name.Trim();
        var tag = await db.Tags.FirstOrDefaultAsync(x => x.Name == tagName);
        if (tag is null)
        {
            tag = new Tag { Name = tagName };
            db.Tags.Add(tag);
            await db.SaveChangesAsync();
        }

        var exists = await db.TaskTags.AnyAsync(tt => tt.TodoTaskId == taskId && tt.TagId == tag.Id);
        if (!exists)
        {
            db.TaskTags.Add(new TaskTag { TodoTaskId = taskId, TagId = tag.Id });
            await db.SaveChangesAsync();
        }

        return NoContent();
    }

    // US21: видалити тег із задачі
    [HttpDelete("tasks/{taskId:int}/tags/{tagId:int}")]
    public async Task<IActionResult> RemoveTagFromTask(int taskId, int tagId)
    {
        var task = await db.TodoTasks.Include(t => t.TodoList)
            .FirstOrDefaultAsync(t => t.Id == taskId && t.TodoList!.OwnerId == UserId);
        if (task is null) return NotFound("Task not found or no permission.");

        var tt = await db.TaskTags.FirstOrDefaultAsync(x => x.TodoTaskId == taskId && x.TagId == tagId);
        if (tt is null) return NotFound();

        db.TaskTags.Remove(tt);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // US18: список усіх тегів у моїх списках/задачах
    [HttpGet("tags")]
    public async Task<IEnumerable<WebApiTagDto>> GetAllTags()
    {
        return await db.TaskTags
            .Where(tt => tt.Task!.TodoList!.OwnerId == UserId || tt.Task!.AssignedUserId == UserId)
            .Select(tt => tt.Tag!)
            .Distinct()
            .OrderBy(t => t.Name)
            .Select(t => new WebApiTagDto { Id = t.Id, Name = t.Name })
            .ToListAsync();
    }
    // PUT /api/tags/{tagId}  — перейменувати тег (глобально)
    [HttpPut("tags/{tagId:int}")]
    public async Task<IActionResult> RenameTag(int tagId, [FromBody] UpdateTagDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest("Tag name is required.");
        var name = dto.Name.Trim();

        // Тег має бути присутній у хоч одній задачі, до якої користувач має доступ
        var canTouch = await db.TaskTags
            .AnyAsync(tt => tt.TagId == tagId &&
                            (tt.Task!.TodoList!.OwnerId == UserId || tt.Task!.AssignedUserId == UserId));
        if (!canTouch) return NotFound();

        var existsSame = await db.Tags.AnyAsync(t => t.Name == name && t.Id != tagId);
        if (existsSame) return Conflict("Tag with this name already exists.");

        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == tagId);
        if (tag is null) return NotFound();

        tag.Name = name;
        await db.SaveChangesAsync();
        return NoContent();
    }

// DELETE /api/tags/{tagId} — видалити тег (глобально, з усіх задач)
    [HttpDelete("tags/{tagId:int}")]
    public async Task<IActionResult> DeleteTag(int tagId)
    {
        var canTouch = await db.TaskTags
            .AnyAsync(tt => tt.TagId == tagId &&
                            (tt.Task!.TodoList!.OwnerId == UserId || tt.Task!.AssignedUserId == UserId));
        if (!canTouch) return NotFound();

        // прибираємо зв'язки, потім тег
        var links = db.TaskTags.Where(tt => tt.TagId == tagId);
        db.TaskTags.RemoveRange(links);

        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == tagId);
        if (tag != null) db.Tags.Remove(tag);

        await db.SaveChangesAsync();
        return NoContent();
    }


    // US19: задачі з певним тегом
    [HttpGet("tags/{tagId:int}/tasks")]
    public async Task<IEnumerable<TaskItemDto>> GetTasksByTag(int tagId)
    {
        return await db.TodoTasks
            .Include(t => t.TodoList)
            .Include(t => t.TaskTags).ThenInclude(x => x.Tag)
            .Where(t => t.TaskTags.Any(tt => tt.TagId == tagId)
                     && (t.TodoList!.OwnerId == UserId || t.AssignedUserId == UserId))
            .Select(t => new TaskItemDto
            {
                Id = t.Id,
                TodoListId = t.TodoListId,
                Title = t.Title,
                Description = t.Description,
                DueDate = t.DueDate,
                Status = t.Status,
                AssignedUserId = t.AssignedUserId,
                Tags = t.TaskTags.Select(tt => new WebApiTagDto { Id = tt.TagId, Name = tt.Tag!.Name }).ToList()
            })
            .ToListAsync();
    }
}
