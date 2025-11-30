namespace TodoListApp.WebApp.Models;

public sealed class ShareDto
{
    public string UserId { get; set; } = string.Empty;
    public string Email  { get; set; } = string.Empty;
    public string Role   { get; set; } = "Reader";
}

public sealed class ShareUpsertRequest
{
    public string Email { get; set; } = string.Empty;
    public string Role  { get; set; } = "Reader";
}