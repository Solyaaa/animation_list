namespace TodoListApp.Domain.Entities;

public sealed class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<TaskTag> TaskTags { get; set; } = new List<TaskTag>();
}