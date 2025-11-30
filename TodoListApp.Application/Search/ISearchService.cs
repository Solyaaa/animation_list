using TodoListApp.Application.Common;
using TodoListApp.Domain.Entities;

namespace TodoListApp.Application.Search;

/// <summary>
/// Пошук задач в межах доступних користувачу списків (власник/поділено) або призначених йому задач.
/// </summary>
public interface ISearchService
{
    Task<PagedResult<TodoTask>> SearchTasksAsync(
        string userId,
        string? query,
        DateTime? createdFromUtc,
        DateTime? createdToUtc,
        DateTime? dueFromUtc,
        DateTime? dueToUtc,
        PagedRequest page,
        CancellationToken ct);
}