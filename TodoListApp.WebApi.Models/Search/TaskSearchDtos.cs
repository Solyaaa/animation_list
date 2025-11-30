using TodoListApp.Application.Tasks;

namespace TodoListApp.WebApi.Models.Search;

public sealed class TaskDtoPagedResponse
{
    public List<TaskDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}