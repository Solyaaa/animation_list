using System.ComponentModel.DataAnnotations;

namespace TodoListApp.Infrastructure.Persistence;

public class ApiKey
{
    public int Id { get; set; }

    [Required]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string AppUserId { get; set; } = string.Empty;
    public AppUser? AppUser { get; set; }

    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime LastUsedAt { get; set; }
    public int UsageCount { get; set; }
}