using TodoListApp.Domain.Entities;
using TodoListApp.WebApi.Models.Tags;
using TaskStatus = TodoListApp.Domain.Entities.TaskStatus;

namespace TodoListApp.WebApi.Models.Tasks;

public class TaskItemDto
{
    public int Id { get; set; }
    public int TodoListId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public TaskStatus Status { get; set; }
    public string AssignedUserId { get; set; } = "";
    public bool IsOverdue => DueDate.HasValue && DueDate.Value.Date < DateTime.UtcNow.Date && Status != TaskStatus.Done;

    public List<TagDto> Tags { get; set; } = new(); // <- ДОДАНО
}

public class CreateTaskDto
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public int ListId { get; set; }
}

public class UpdateTaskDto
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public TaskStatus Status { get; set; }
    public string? AssignedUserId { get; set; }
}


