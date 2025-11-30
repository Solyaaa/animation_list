namespace TodoListApp.Domain.Entities;

public class Comment
{
    public int Id { get; set; }
    public int TodoTaskId { get; set; }
    public TodoTask? TodoTask { get; set; }

    public string AuthorId { get; set; } = "";
    public string Text { get; set; } = "";

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedUtc { get; set; }
}