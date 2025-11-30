using TodoListApp.Application.Common;

namespace TodoListApp.Application.Lists;

public sealed record ListDto(int Id, string Title, string? Description)
{

    public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }

}

public interface IListService
{
    Task<PagedResult<ListDto>> GetMyListsAsync(string userId, PagedRequest req, CancellationToken ct);
    Task<int> CreateAsync(string userId, string title, string? description, CancellationToken ct);
    Task UpdateAsync(string userId, int id, string title, string? description, CancellationToken ct);
    Task DeleteAsync(string userId, int id, CancellationToken ct);
}