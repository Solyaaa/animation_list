
using System.Text.Json.Serialization;

namespace TodoListApp.WebApp.Models;

public class TagDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public class AddTagDto
{
    public string Name { get; set; } = "";
}


public class TagDtoPagedResponse
{
    [JsonPropertyName("items")]
    public List<TagDto> Items { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }
}
public class TagEditVm
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
