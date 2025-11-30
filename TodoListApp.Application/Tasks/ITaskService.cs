using TodoListApp.Application.Common;
using System.Collections.Generic; // Додано для List<T>
// using TodoListApp.WebApi.Models.Tags; // <-- НЕПРАВИЛЬНИЙ USING ВИДАЛЕНО

using DomainTaskStatus = TodoListApp.Domain.Entities.TaskStatus;

namespace TodoListApp.Application.Tasks;

// ВИЗНАЧЕННЯ TagDto ПЕРЕМІЩЕНО СЮДИ
public sealed record TagDto(int Id, string Name);

public sealed record TaskDto(
    int Id,
    int ListId,
    string Title,
    string? Description,
    DateTime? DueDateUtc,
    string AssigneeId,
    string Status,
    bool IsOverdue,
    List<TagDto> Tags);

public interface ITaskService
{
    Task<PagedResult<TaskDto>> GetByListAsync(string userId, int listId, PagedRequest req, CancellationToken ct);
    Task<TaskDto> GetByIdAsync(string userId, int taskId, CancellationToken ct);
    Task<TaskDto> CreateTaskAsync(string userId, int listId, string title, string? description, List<int>? tagIds, CancellationToken ct);
    Task UpdateAsync(string userId, int taskId, string title, string? description, DateTime? dueUtc, string? assigneeId, DomainTaskStatus? status, CancellationToken ct);
    Task DeleteAsync(string userId, int taskId, CancellationToken ct);
    Task<PagedResult<TaskDto>> GetAssignedAsync(string userId, DomainTaskStatus? status, string? sortBy, bool desc, PagedRequest req, CancellationToken ct);
    Task UpdateStatusAsync(string userId, int taskId, DomainTaskStatus status, CancellationToken ct);
}