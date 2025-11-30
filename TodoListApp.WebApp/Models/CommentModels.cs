using System.Text.Json.Serialization;

namespace TodoListApp.WebApp.Models;

public class CommentDto
{
    public int Id { get; set; }
    public int TodoTaskId { get; set; }
    public string AuthorId { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
    [JsonPropertyName("created")]
    public DateTime? Created { get; set; }
    [JsonPropertyName("createdAt")]
    public DateTime? CreatedAt { get; set; }
    [JsonPropertyName("createdOn")]
    public DateTime? CreatedOn { get; set; }
}

public class CreateCommentDto
{
    public string Text { get; set; } = "";
}

public class UpdateCommentDto
{
    public string Text { get; set; } = "";
}