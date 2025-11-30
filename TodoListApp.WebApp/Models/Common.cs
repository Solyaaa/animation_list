namespace TodoListApp.WebApp.Models;

public record PagedResponse<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);