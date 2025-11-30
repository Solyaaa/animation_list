using TodoListApp.WebApp.Models;

namespace TodoListApp.WebApp.Services;

public interface IApiClient
{
    // ===== Auth =====
    Task<string?> LoginAsync(string email, string password, CancellationToken ct = default);
    Task<bool> RegisterAsync(string email, string password, CancellationToken ct = default);

    // ===== Lists =====
    Task<List<ListItemDto>> GetListsAsync(CancellationToken ct = default);
    Task CreateListAsync(CreateListDto dto, CancellationToken ct = default);
    Task UpdateListAsync(int id, UpdateListDto dto, CancellationToken ct = default);
    Task DeleteListAsync(int id, CancellationToken ct = default);

    // ===== Tasks =====
    Task<List<TaskItemDto>> GetTasksInListAsync(int listId, CancellationToken ct = default);
    Task<TaskItemDto?> GetTaskAsync(int taskId, CancellationToken ct = default);
    Task CreateTaskAsync(int listId, CreateTaskDto dto, CancellationToken ct = default);

    // Дві версії — під обидва можливі виклики з твого TasksController
    Task UpdateTaskAsync(int taskId, UpdateTaskDto dto, CancellationToken ct = default);
    Task UpdateTaskAsync(int taskId, string title, string? description, DateTime? dueDate, int? status, CancellationToken ct = default);

    Task ChangeStatusAsync(int taskId, int status, CancellationToken ct = default);

    // ===== Comments =====
    Task<List<CommentDto>> GetCommentsAsync(int taskId, CancellationToken ct = default);
    Task AddCommentAsync(int taskId, string text, CancellationToken ct = default);
    Task EditCommentAsync(int taskId, int commentId, string text, CancellationToken ct = default);
    Task DeleteCommentAsync(int taskId, int commentId, CancellationToken ct = default);

    // ===== Tags =====
    Task<TagDtoPagedResponse> GetAllTagsPagedAsync(int page, int pageSize, string? query = null, CancellationToken ct = default);
    Task<List<TaskItemDto>> GetTasksByTagAsync(int tagId, CancellationToken ct = default);
    Task UpdateTagAsync(int id, string name, CancellationToken ct = default);
    Task DeleteTagAsync(int id, CancellationToken ct = default);
    Task AddTagAsync(int taskId, string name, CancellationToken ct = default);
    Task RemoveTagAsync(int taskId, int tagId, CancellationToken ct = default);

    // ===== Assigned =====
    Task<List<TaskItemDto>> GetAssignedAsync(int? status = null, string? sortBy = null, CancellationToken ct = default);

    // ===== Search =====
    Task<List<TaskItemDto>> SearchAsync(string? title, DateTime? createdFrom, DateTime? dueTo, CancellationToken ct = default);
}
