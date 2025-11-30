using Microsoft.EntityFrameworkCore;
using TodoListApp.Application.Common;
using TodoListApp.Application.Lists;
using TodoListApp.Infrastructure.Persistence;
using TodoListEntity = TodoListApp.Domain.Entities.TodoList;

namespace TodoListApp.Infrastructure.Services;

public sealed class ListService : IListService
{
    private readonly AppDbContext _db;
    public ListService(AppDbContext db) => _db = db;

    public async Task<PagedResult<ListDto>> GetMyListsAsync(string userId, PagedRequest req, CancellationToken ct)
    {
        var q = _db.TodoLists.Where(x => x.OwnerId == userId);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.Id)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(x => new ListDto(x.Id, x.Title, x.Description))
            .ToListAsync(ct);

        return new(items, total, req.Page, req.PageSize);
    }

    public async Task<int> CreateAsync(string userId, string title, string? description, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required", nameof(title));

        var entity = new TodoListEntity
        {
            Title = title.Trim(),
            Description = description,
            OwnerId = userId
        };

        _db.TodoLists.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task UpdateAsync(string userId, int id, string title, string? description, CancellationToken ct)
    {
        var entity = await _db.TodoLists.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId, ct)
                     ?? throw new KeyNotFoundException("List not found");

        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required", nameof(title));
        entity.Title = title.Trim();
        entity.Description = description;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string userId, int id, CancellationToken ct)
    {
        var entity = await _db.TodoLists.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId, ct)
                     ?? throw new KeyNotFoundException("List not found");

        _db.TodoLists.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }
}
