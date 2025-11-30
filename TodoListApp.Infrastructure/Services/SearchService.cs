/*using Microsoft.EntityFrameworkCore;
using TodoListApp.Application.Common;
using TodoListApp.Application.Search;
using TodoListApp.Domain.Entities;
using TodoListApp.Infrastructure.Persistence;

namespace TodoListApp.Infrastructure.Services;

public sealed class SearchService : ISearchService
{
    private readonly AppDbContext _db;
    public SearchService(AppDbContext db) => _db = db;

    public async Task<PagedResult<TodoTask>> SearchTasksAsync(
        string userId, string? query,
        DateTime? createdFromUtc, DateTime? createdToUtc,
        DateTime? dueFromUtc, DateTime? dueToUtc,
        PagedRequest page, CancellationToken ct)
    {
        var accessibleListIds = _db.TodoLists
            .Where(l => l.OwnerId == userId)
            .Select(l => l.Id)
            .Union(_db.ListShares
                .Where(s => s.UserId == userId)
                .Select(s => s.TodoListId))
            .Distinct();

        var q = _db.TodoTasks.AsNoTracking()
            .Where(t => accessibleListIds.Contains(t.TodoListId) || t.AssigneeId == userId);

        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(t => t.Title.Contains(query));

        if (createdFromUtc.HasValue) q = q.Where(t => t.CreatedAtUtc >= createdFromUtc.Value);
        if (createdToUtc.HasValue)   q = q.Where(t => t.CreatedAtUtc <= createdToUtc.Value);
        if (dueFromUtc.HasValue)     q = q.Where(t => t.DueDateUtc != null && t.DueDateUtc >= dueFromUtc.Value);
        if (dueToUtc.HasValue)       q = q.Where(t => t.DueDateUtc != null && t.DueDateUtc <= dueToUtc.Value);

        q = q.OrderByDescending(t => t.CreatedAtUtc);

        var total = await q.CountAsync(ct);
        var items = await q.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(ct);

        return new PagedResult<TodoTask>(items, total, page.Page, page.PageSize);
    }
}
*/