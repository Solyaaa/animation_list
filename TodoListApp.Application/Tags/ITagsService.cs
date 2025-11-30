/*using TodoListApp.Application.Common;
using TodoListApp.Domain.Entities;

namespace TodoListApp.Application.Tags;

public interface ITagsService
{

    Task<IReadOnlyList<Tag>> GetTaskTagsAsync(string userId, int taskId, CancellationToken ct);


    Task<PagedResult<Tag>> GetAllTagsAsync(string userId, PagedRequest page, CancellationToken ct);


    Task<PagedResult<TodoTask>> GetTasksByTagAsync(string userId, int tagId, PagedRequest page, CancellationToken ct);

    Task AddTagToTaskAsync(string userId, int taskId, string tagName, CancellationToken ct);
    Task RemoveTagFromTaskAsync(string userId, int taskId, int tagId, CancellationToken ct);


    Task<Tag> GetTagByIdAsync(string userId, int tagId, CancellationToken ct);
    Task<Tag> CreateTagAsync(string userId, string tagName, CancellationToken ct);
    Task UpdateTagAsync(string userId, int tagId, string newTagName, CancellationToken ct);
    Task DeleteTagAsync(string userId, int tagId, CancellationToken ct);
}
*/