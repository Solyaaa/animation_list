using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoListApp.Infrastructure.Persistence;
using TodoListApp.WebApi.Models.Comments;
using TodoListApp.Domain.Entities;

namespace TodoListApp.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class CommentsController(AppDbContext db) : ControllerBase
{
    private string UserId => User.FindFirst("uid")?.Value ?? "";

    // US22: перегляд коментарів задачі
    [HttpGet("tasks/{taskId:int}/comments")]
    public async Task<IEnumerable<CommentDto>> GetTaskComments(int taskId)
    {
        // Доступ: власник списку або призначений користувач
        var ok = await db.TodoTasks.Include(t => t.TodoList)
            .AnyAsync(t => t.Id == taskId && (t.TodoList!.OwnerId == UserId || t.AssignedUserId == UserId));
        if (!ok) return Enumerable.Empty<CommentDto>();

        return await db.Comments.Where(c => c.TodoTaskId == taskId)
            .OrderBy(c => c.CreatedUtc)
            .Select(c => new CommentDto
            {
                Id = c.Id, TodoTaskId = c.TodoTaskId, AuthorId = c.AuthorId,
                Text = c.Text, CreatedUtc = c.CreatedUtc, UpdatedUtc = c.UpdatedUtc
            })
            .ToListAsync();
    }

    // US23: додати коментар (власник або призначений)
    [HttpPost("tasks/{taskId:int}/comments")]
    public async Task<IActionResult> AddComment(int taskId, [FromBody] CreateCommentDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Text)) return BadRequest("Text is required.");

        var task = await db.TodoTasks.Include(t => t.TodoList)
            .FirstOrDefaultAsync(t => t.Id == taskId && (t.TodoList!.OwnerId == UserId || t.AssignedUserId == UserId));
        if (task is null) return NotFound();

        var c = new Comment { TodoTaskId = taskId, AuthorId = UserId, Text = dto.Text.Trim() };
        db.Comments.Add(c);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetTaskComments), new { taskId }, c.Id);
    }

    // US25: редагувати свій коментар; US24: власник списку може редагувати/видаляти будь-який
    [HttpPut("comments/{id:int}")]
    public async Task<IActionResult> EditComment(int id, [FromBody] UpdateCommentDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Text)) return BadRequest("Text is required.");

        var c = await db.Comments.Include(x => x.TodoTask)!.ThenInclude(t => t!.TodoList)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        var isOwner = c.TodoTask!.TodoList!.OwnerId == UserId;
        var isAuthor = c.AuthorId == UserId;
        if (!isOwner && !isAuthor) return NotFound(); // ховаємо ресурс, якщо нема прав

        c.Text = dto.Text.Trim();
        c.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("comments/{id:int}")]
    public async Task<IActionResult> DeleteComment(int id)
    {
        var c = await db.Comments.Include(x => x.TodoTask)!.ThenInclude(t => t!.TodoList)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        var isOwner = c.TodoTask!.TodoList!.OwnerId == UserId;
        var isAuthor = c.AuthorId == UserId;
        if (!isOwner && !isAuthor) return NotFound();

        db.Comments.Remove(c);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

