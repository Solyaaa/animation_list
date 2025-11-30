namespace TodoListApp.WebApi.Models.Comments;

public class CommentDto
{
    public int Id { get; set; }
    public int TodoTaskId { get; set; }
    public string AuthorId { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
}

public class CreateCommentDto
{
    public string Text { get; set; } = "";
}

public class UpdateCommentDto
{
    public string Text { get; set; } = "";
}