namespace TodoListApp.WebApp.Models;

public enum TaskStatus { Pending = 0, InProgress = 1, Done = 2 }

public class TaskItemDto
{
    public int Id { get; set; }
    public int TodoListId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public Domain.Entities.TaskStatus Status { get; set; }
    public string AssignedUserId { get; set; } = "";
    public bool IsOverdue { get; set; }
    public List<TagDto> Tags { get; set; } = new(); // <- ДОДАНО
}

public class CreateTaskDto
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
}

