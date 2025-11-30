using System.Collections.Generic;
using System.Linq;

namespace TodoListApp.Application.Common;

/// <summary>Результат з пагінацією.</summary>
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; }
    public int TotalCount { get; }
    public int Page { get; }
    public int PageSize { get; }

    public PagedResult(IEnumerable<T> items, int totalCount, int page, int pageSize)
    {
        Items = items.ToList();
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }

    public static PagedResult<T> Empty(int page, int pageSize) =>
        new PagedResult<T>(System.Array.Empty<T>(), 0, page, pageSize);
}