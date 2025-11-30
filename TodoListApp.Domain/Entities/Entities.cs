namespace TodoListApp.Domain.Entities;

public enum TaskStatus {  Pending = 0, InProgress = 1, Done = 2 }

public sealed class TodoList
{ public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string OwnerId { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TodoTask> Tasks { get; set; } = new List<TodoTask>();
}


public sealed class TodoTask
{
    public int Id { get; set; }
    public int TodoListId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.Pending;

    public string AssignedUserId { get; set; } = "";
    public TodoList? TodoList { get; set; }

    public ICollection<TaskTag> TaskTags { get; set; } = new List<TaskTag>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
}