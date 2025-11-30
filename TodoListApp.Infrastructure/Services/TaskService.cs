/*using Microsoft.EntityFrameworkCore;
using TodoListApp.Application.Common;
using TodoListApp.Application.Tasks;
using TodoListApp.Domain.Entities;
using TodoListApp.Infrastructure.Persistence;

// аліаси, щоб уникнути конфліктів назв
using DomainTaskStatus = TodoListApp.Domain.Entities.TaskStatus;
using TodoTaskEntity   = TodoListApp.Domain.Entities.TodoTask;
using ShareRole       = TodoListApp.Domain.Entities.ShareRole;

namespace TodoListApp.Infrastructure.Services;

public sealed class TaskService : ITaskService
{
    private readonly AppDbContext _db;
    public TaskService(AppDbContext db) => _db = db;

    private static bool IsActive(DomainTaskStatus s) =>
        s is DomainTaskStatus.New or DomainTaskStatus.InProgress or DomainTaskStatus.Blocked;

    private static TaskDto Map(TodoTaskEntity t)
    {
        var overdue = t.DueDateUtc.HasValue &&
                      t.DueDateUtc.Value < DateTime.UtcNow &&
                      t.Status != DomainTaskStatus.Done;

        return new TaskDto(
            t.Id,
            t.TodoListId,
            t.Title,
            t.Description,
            t.DueDateUtc,
            t.AssigneeId,
            t.Status.ToString(),
            overdue,
            t.Tags.Select(tag => new TagDto(tag.Id, tag.Name)).ToList()
        );
    }

    private async Task<bool> CanViewListAsync(string userId, int listId, CancellationToken ct)
    {
        return await _db.TodoLists.AnyAsync(l => l.Id == listId && l.OwnerId == userId, ct)
            || await _db.ListShares.AnyAsync(s => s.TodoListId == listId && s.UserId == userId, ct);
    }

    private async Task<bool> CanEditListAsync(string userId, int listId, CancellationToken ct)
    {
        if (await _db.TodoLists.AnyAsync(l => l.Id == listId && l.OwnerId == userId, ct))
            return true;

        // редагувати може лише власник або користувач з роллю Writer
        return await _db.ListShares.AnyAsync(
            s => s.TodoListId == listId && s.UserId == userId && s.Role == ShareRole.Writer, ct);
    }

    public async Task<PagedResult<TaskDto>> GetByListAsync(string userId, int listId, PagedRequest req, CancellationToken ct)
    {
        if (!await CanViewListAsync(userId, listId, ct)) throw new UnauthorizedAccessException();

        // Додаємо .Include(t => t.Tags), щоб завантажити пов'язані теги
        var q = _db.TodoTasks
            .Include(t => t.Tags)
            .Where(t => t.TodoListId == listId);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(t => t.Id)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .ToListAsync(ct);

        return new(items.Select(Map).ToList(), total, req.Page, req.PageSize);
    }

    public async Task<TaskDto> GetByIdAsync(string userId, int taskId, CancellationToken ct)
    {
        // Додаємо .Include(t => t.Tags)
        var t = await _db.TodoTasks
                    .Include(t => t.Tags)
                    .FirstOrDefaultAsync(x => x.Id == taskId, ct)
                ?? throw new KeyNotFoundException("Task not found");

        if (!await CanViewListAsync(userId, t.TodoListId, ct)) throw new UnauthorizedAccessException();
        return Map(t);
    }
    // ++++ ADD THIS NEW METHOD IN ITS PLACE ++++
    // ++++ ADD THIS NEW METHOD IN ITS PLACE ++++
    public async Task<TaskDto> CreateTaskAsync(string userId, int listId, string title, string? description, List<int>? tagIds, CancellationToken ct)
    {
        if (!await CanEditListAsync(userId, listId, ct)) throw new UnauthorizedAccessException();
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required", nameof(title));

        var entity = new TodoTaskEntity
        {
            TodoListId = listId,
            Title = title.Trim(),
            Description = description,
            Status = DomainTaskStatus.New,
            AssigneeId = userId // US07: The author becomes the assignee by default
        };

        // Спочатку додаємо і зберігаємо саме завдання, щоб отримати його ID
        _db.TodoTasks.Add(entity);
        await _db.SaveChangesAsync(ct);

        // Тепер, коли у 'entity' є ID, працюємо з тегами
        if (tagIds != null && tagIds.Any())
        {
            // Перевіряємо, що всі передані теги існують
            var tagsCount = await _db.Tags.CountAsync(t => tagIds.Contains(t.Id), ct);
            if (tagsCount != tagIds.Count)
            {
                throw new KeyNotFoundException("One or more tags not found.");
            }

            // Створюємо зв'язки вручну
            var taskTags = tagIds.Select(tagId => new TaskTag { TaskId = entity.Id, TagId = tagId }).ToList();
            _db.TaskTags.AddRange(taskTags);

            // Зберігаємо зв'язки
            await _db.SaveChangesAsync(ct);
        }

        // Повертаємо DTO створеного завдання
        return Map(entity);
    }

    public async Task UpdateAsync(string userId, int taskId, string title, string? description, DateTime? dueUtc, string? assigneeId, DomainTaskStatus? status, CancellationToken ct)
    {
        var t = await _db.TodoTasks.FirstOrDefaultAsync(x => x.Id == taskId, ct)
                ?? throw new KeyNotFoundException("Task not found");

        if (!await CanEditListAsync(userId, t.TodoListId, ct)) throw new UnauthorizedAccessException();

        if (!string.IsNullOrWhiteSpace(title)) t.Title = title.Trim();
        t.Description = description;
        t.DueDateUtc = dueUtc;
        if (assigneeId is not null) t.AssigneeId = assigneeId;
        if (status is not null) t.Status = status.Value;

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string userId, int taskId, CancellationToken ct)
    {
        var t = await _db.TodoTasks.FirstOrDefaultAsync(x => x.Id == taskId, ct)
                ?? throw new KeyNotFoundException("Task not found");

        if (!await CanEditListAsync(userId, t.TodoListId, ct)) throw new UnauthorizedAccessException();

        _db.TodoTasks.Remove(t);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<TaskDto>> GetAssignedAsync(string userId, DomainTaskStatus? status, string? sortBy, bool desc, PagedRequest req, CancellationToken ct)
    {
        var q = _db.TodoTasks.Where(t => t.AssigneeId == userId);

        if (status is not null) q = q.Where(t => t.Status == status);
        else q = q.Where(t => IsActive(t.Status)); // US12: за замовчуванням — активні

        q = (sortBy?.ToLowerInvariant()) switch
        {
            "name" => (desc ? q.OrderByDescending(t => t.Title) : q.OrderBy(t => t.Title)),
            "due"  => (desc ? q.OrderByDescending(t => t.DueDateUtc) : q.OrderBy(t => t.DueDateUtc)),
            _      => q.OrderByDescending(t => t.Id)
        };

        var total = await q.CountAsync(ct);
        var items = await q.Skip((req.Page - 1) * req.PageSize)
                           .Take(req.PageSize)
                           .ToListAsync(ct);

        return new(items.Select(Map).ToList(), total, req.Page, req.PageSize);
    }

    public async Task UpdateStatusAsync(string userId, int taskId, DomainTaskStatus status, CancellationToken ct)
    {
        var t = await _db.TodoTasks.FirstOrDefaultAsync(x => x.Id == taskId, ct)
                ?? throw new KeyNotFoundException("Task not found");

        var canEdit = t.AssigneeId == userId || await CanEditListAsync(userId, t.TodoListId, ct);
        if (!canEdit) throw new UnauthorizedAccessException();

        t.Status = status;
        await _db.SaveChangesAsync(ct);
    }
}
*/
