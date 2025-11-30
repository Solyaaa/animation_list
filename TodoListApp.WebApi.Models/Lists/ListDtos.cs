using System.ComponentModel.DataAnnotations;
namespace TodoListApp.WebApi.Models.Lists;

public class ListItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TasksCount { get; set; }
}

public class CreateListDto
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
}

public class UpdateListDto
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
}
