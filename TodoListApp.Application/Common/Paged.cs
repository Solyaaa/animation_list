namespace TodoListApp.Application.Common;

/// <summary>Параметри пагінації.</summary>
public sealed class PagedRequest
{
    public int Page { get; }
    public int PageSize { get; }

    public PagedRequest(int page, int pageSize)
    {
        Page = page < 1 ? 1 : page;
        PageSize = pageSize is < 1 or > 200 ? 20 : pageSize;
    }
}

