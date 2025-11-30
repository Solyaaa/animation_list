namespace TodoListApp.WebApi.Models.Shares;

public sealed class ShareDto
{
    public string UserId { get; init; } = default!;
    public string Email  { get; init; } = default!;
    public string Role   { get; init; } = default!;
}

public sealed class ShareUpsertRequest
{
    public string Email { get; init; } = default!;
    public string Role  { get; init; } = default!;
}