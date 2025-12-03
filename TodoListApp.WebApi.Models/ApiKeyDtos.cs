namespace TodoListApp.WebApi.Models;

public class CreateApiKeyDto
{
    public string Name { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
}

public class ApiKeyDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime LastUsedAt { get; set; }
    public int UsageCount { get; set; }
}