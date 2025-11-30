using System.Text.Json.Serialization;

namespace TodoListApp.WebApp.Models
{
    public class UpdateTaskDto
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public DateTime? DueDate { get; set; }
        public Domain.Entities.TaskStatus Status { get; set; }
        public string? AssignedUserId { get; set; }
    }
    public class TokenDto
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;
    }
}