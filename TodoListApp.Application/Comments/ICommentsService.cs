using TodoListApp.Application.Common;
using TodoListApp.Domain.Entities;

namespace TodoListApp.Application.Comments;

public interface ICommentsService
{
    Task<PagedResult<Comment>> GetTaskCommentsAsync(string userId, int taskId, PagedRequest page, CancellationToken ct);
    Task<int> AddAsync(string userId, int taskId, string text, CancellationToken ct);
    Task RemoveAsync(string userId, int taskId, int commentId, CancellationToken ct);
}