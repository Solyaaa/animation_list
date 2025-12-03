// TaskDtos.cs - виправлена версія
using System.Text.Json.Serialization;
using TodoListApp.Domain.Entities;
using TodoListApp.WebApi.Models.Tags;

namespace TodoListApp.WebApi.Models.Tasks;

public class TaskItemDto
{
    public int Id { get; set; }
    public int TodoListId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }

    // STRING, не enum
    public string Status { get; set; } = "";

    public string AssignedUserId { get; set; } = "";

    // Властивість для обчислення
    public bool IsOverdue
    {
        get
        {
            if (!DueDate.HasValue) return false;
            if (Status?.ToLower() == "done") return false;
            return DueDate.Value.Date < DateTime.UtcNow.Date;
        }
    }

    public List<TagDto> Tags { get; set; } = new();
}

public class CreateTaskDto
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public int ListId { get; set; }
}

// Оновіть UpdateTaskDto теж
public class UpdateTaskDto
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }

    // Теж string
    public string Status { get; set; } = "";

    public string? AssignedUserId { get; set; }
}