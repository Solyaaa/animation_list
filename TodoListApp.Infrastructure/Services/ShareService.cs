/*using Microsoft.EntityFrameworkCore;
using TodoListApp.Application.Shares;
using TodoListApp.Domain.Entities;
using TodoListApp.Infrastructure.Persistence;

namespace TodoListApp.Infrastructure.Services;

public sealed class ShareService : IShareService
{
    private readonly AppDbContext _db;
    public ShareService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<(string UserId, string Email, ShareRole Role)>>
        GetSharesAsync(string requesterId, int listId, CancellationToken ct)
    {
        var list = await _db.TodoLists.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == listId, ct)
            ?? throw new KeyNotFoundException();

        // Перегляд дозволено власнику або Writer цього списку
        if (list.OwnerId != requesterId)
        {
            var myShare = await _db.ListShares.AsNoTracking()
                .FirstOrDefaultAsync(s => s.TodoListId == listId && s.UserId == requesterId, ct);

            if (myShare is null || myShare.Role != ShareRole.Writer)
                throw new UnauthorizedAccessException();
        }

        var collaborators = await (
            from s in _db.ListShares.AsNoTracking().Where(x => x.TodoListId == listId)
            join u in _db.Users.AsNoTracking() on s.UserId equals u.Id into gj
            from u in gj.DefaultIfEmpty()
            select new { s.UserId, Email = u!.Email!, s.Role }
        ).ToListAsync(ct);

        var ownerEmail = await _db.Users.AsNoTracking()
            .Where(u => u.Id == list.OwnerId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(ct);

        var result = new List<(string, string, ShareRole)>();
        if (!string.IsNullOrEmpty(ownerEmail))
            result.Add((list.OwnerId, ownerEmail!, ShareRole.Writer));

        foreach (var c in collaborators)
            result.Add((c.UserId, c.Email ?? string.Empty, c.Role));

        return result;
    }

    public async Task AddOrUpdateAsync(string ownerId, int listId, string email, ShareRole role, CancellationToken ct)
    {
        var list = await _db.TodoLists.FirstOrDefaultAsync(l => l.Id == listId, ct)
            ?? throw new KeyNotFoundException();

        if (list.OwnerId != ownerId)
            throw new UnauthorizedAccessException();

        var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == email, ct)
            ?? throw new KeyNotFoundException();

        var existing = await _db.ListShares
            .SingleOrDefaultAsync(s => s.TodoListId == listId && s.UserId == user.Id, ct);

        if (existing is null)
            _db.ListShares.Add(new ListShare { TodoListId = listId, UserId = user.Id, Role = role });
        else
        {
            existing.Role = role;
            _db.ListShares.Update(existing);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(string ownerId, int listId, string userId, CancellationToken ct)
    {
        var list = await _db.TodoLists.FirstOrDefaultAsync(l => l.Id == listId, ct)
            ?? throw new KeyNotFoundException();

        if (list.OwnerId != ownerId)
            throw new UnauthorizedAccessException();

        var share = await _db.ListShares
            .SingleOrDefaultAsync(s => s.TodoListId == listId && s.UserId == userId, ct)
            ?? throw new KeyNotFoundException();

        _db.ListShares.Remove(share);
        await _db.SaveChangesAsync(ct);
    }
}
*/
