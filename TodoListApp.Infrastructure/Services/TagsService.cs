/*using Microsoft.EntityFrameworkCore;
using TodoListApp.Application.Common;
using TodoListApp.Application.Tags;
using TodoListApp.Domain.Entities;
using TodoListApp.Infrastructure.Persistence;

namespace TodoListApp.Infrastructure.Services;

public sealed class TagsService : ITagsService
{
    private readonly AppDbContext _db;
    public TagsService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Tag>> GetTaskTagsAsync(string userId, int taskId, CancellationToken ct)
    {
        var task = await _db.TodoTasks
            .Include(t => t.TodoList)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new KeyNotFoundException();

        // доступ: власник або є шаринг (будь-яка роль)
        var owner = task.TodoList!.OwnerId == userId;
        var share = await _db.ListShares
            .FirstOrDefaultAsync(s => s.TodoListId == task.TodoListId && s.UserId == userId, ct);

        if (!owner && share is null) throw new UnauthorizedAccessException();

        var tags = await _db.TaskTags
            .Where(tt => tt.TaskId == taskId)
            .Select(tt => tt.Tag)
            .ToListAsync(ct);

        return tags;
    }

    public async Task<PagedResult<Tag>> GetAllTagsAsync(string userId, PagedRequest page, CancellationToken ct)
    {
        // Нова, правильна логіка: просто беремо всі теги з таблиці Tag,
        // які були створені будь-яким користувачем.
        var baseQuery = _db.Tags.AsQueryable();

        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderBy(t => t.Name)
            .Skip((page.Page - 1) * page.PageSize)
            .Take(page.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Tag>(items, total, page.Page, page.PageSize);
    }

    public async Task<PagedResult<TodoTask>> GetTasksByTagAsync(string userId, int tagId, PagedRequest page, CancellationToken ct)
    {
        var accessibleListIds = await _db.TodoLists
            .Where(l => l.OwnerId == userId)
            .Select(l => l.Id)
            .Union(_db.ListShares.Where(s => s.UserId == userId).Select(s => s.TodoListId))
            .ToListAsync(ct);

        var baseQuery = _db.TaskTags
            .Where(tt => tt.TagId == tagId && accessibleListIds.Contains(tt.Task.TodoListId))
            .Select(tt => tt.Task);

        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderByDescending(t => t.DueDateUtc.HasValue)
            .ThenBy(t => t.DueDateUtc)
            .Skip((page.Page - 1) * page.PageSize)
            .Take(page.PageSize)
            .ToListAsync(ct);

        return new PagedResult<TodoTask>(items, total, page.Page, page.PageSize);
    }

    public async Task AddTagToTaskAsync(string userId, int taskId, string tagName, CancellationToken ct)
    {
        var task = await _db.TodoTasks
            .Include(t => t.TodoList)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new KeyNotFoundException();

        var isOwner = task.TodoList!.OwnerId == userId;
        var share = await _db.ListShares
            .FirstOrDefaultAsync(s => s.TodoListId == task.TodoListId && s.UserId == userId, ct);


        if (!isOwner && (share is null || share.Role != ShareRole.Writer))
            throw new UnauthorizedAccessException();

        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Name == tagName, ct);
        if (tag is null)
        {
            tag = new Tag { Name = tagName };
            _db.Tags.Add(tag);
            await _db.SaveChangesAsync(ct);
        }

        var exists = await _db.TaskTags
            .AnyAsync(tt => tt.TaskId == taskId && tt.TagId == tag.Id, ct);

        if (!exists)
        {
            _db.TaskTags.Add(new TaskTag { TaskId = taskId, TagId = tag.Id });
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task RemoveTagFromTaskAsync(string userId, int taskId, int tagId, CancellationToken ct)
    {
        var task = await _db.TodoTasks
            .Include(t => t.TodoList)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new KeyNotFoundException();

        var isOwner = task.TodoList!.OwnerId == userId;
        var share = await _db.ListShares
            .FirstOrDefaultAsync(s => s.TodoListId == task.TodoListId && s.UserId == userId, ct);

        if (!isOwner && (share is null || share.Role != ShareRole.Writer))
            throw new UnauthorizedAccessException();

        var link = await _db.TaskTags.FirstOrDefaultAsync(
            tt => tt.TaskId == taskId && tt.TagId == tagId, ct);

        if (link is null) throw new KeyNotFoundException();

        _db.TaskTags.Remove(link);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Tag> CreateTagAsync(string userId, string tagName, CancellationToken ct)
    {

        var existingTag = await _db.Tags.FirstOrDefaultAsync(t => t.Name == tagName.Trim(), ct);
        if (existingTag != null)
        {
            return existingTag;
        }


        var newTag = new Tag { Name = tagName.Trim() };
        _db.Tags.Add(newTag);
        await _db.SaveChangesAsync(ct);
        return newTag;
    }

    public async Task UpdateTagAsync(string userId, int tagId, string newTagName, CancellationToken ct)
    {
        var tag = await GetTagByIdAsync(userId, tagId, ct);
        tag.Name = newTagName.Trim();
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteTagAsync(string userId, int tagId, CancellationToken ct)
    {
        var tag = await GetTagByIdAsync(userId, tagId, ct);
        _db.Tags.Remove(tag);
        await _db.SaveChangesAsync(ct);
    }
}
*/
