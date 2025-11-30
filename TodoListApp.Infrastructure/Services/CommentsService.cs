/*using Microsoft.EntityFrameworkCore;
using TodoListApp.Application.Comments;
using TodoListApp.Application.Common;
using TodoListApp.Domain.Entities;
using TodoListApp.Infrastructure.Persistence;

namespace TodoListApp.Infrastructure.Services;

public sealed class CommentsService : ICommentsService
{
    private readonly AppDbContext _db;
    public CommentsService(AppDbContext db) => _db = db;

    private async Task<(TodoTask task, bool canWrite)> EnsureAccessAsync(string userId, int taskId, bool forWrite, CancellationToken ct)
    {
        var task = await _db.TodoTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct)
                   ?? throw new KeyNotFoundException();

        var list = await _db.TodoLists.AsNoTracking().FirstAsync(l => l.Id == task.TodoListId, ct);

        if (list.OwnerId == userId)
            return (task, true);

        var share = await _db.ListShares.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TodoListId == list.Id && s.UserId == userId, ct);

        if (share is null)
            throw new UnauthorizedAccessException();

        // CommentsService.cs
        var canWrite = share.Role == ShareRole.Writer;


        if (forWrite && !canWrite)
            throw new UnauthorizedAccessException();

        return (task, canWrite);
    }

    public async Task<PagedResult<Comment>> GetTaskCommentsAsync(string userId, int taskId, PagedRequest page, CancellationToken ct)
    {
        await EnsureAccessAsync(userId, taskId, forWrite: false, ct);

        var query = _db.Comments.AsNoTracking()
            .Where(c => c.TaskId == taskId)
            .OrderByDescending(c => c.CreatedAtUtc);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page.Page - 1) * page.PageSize)
            .Take(page.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Comment>(items, total, page.Page, page.PageSize);
    }

    public async Task<int> AddAsync(string userId, int taskId, string text, CancellationToken ct)
    {
        await EnsureAccessAsync(userId, taskId, forWrite: true, ct);

        var entity = new Comment
        {
            TaskId = taskId,
            AuthorId = userId,
            Text = text.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Comments.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task RemoveAsync(string userId, int taskId, int commentId, CancellationToken ct)
    {
        var (_, canWrite) = await EnsureAccessAsync(userId, taskId, forWrite: false, ct);

        var entity = await _db.Comments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.TaskId == taskId, ct)
            ?? throw new KeyNotFoundException();


        if (entity.AuthorId != userId && !canWrite)
            throw new UnauthorizedAccessException();

        _db.Comments.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }
}
*/
