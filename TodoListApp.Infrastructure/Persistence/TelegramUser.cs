using System.ComponentModel.DataAnnotations;

namespace TodoListApp.Infrastructure.Persistence;

public class TelegramUser
{
    public int Id { get; set; }

    [Required]
    public long TelegramUserId { get; set; }

    public string? TelegramUsername { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    [Required]
    public string AppUserId { get; set; } = "";

    public int? ApiKeyId { get; set; }
    public ApiKey? ApiKey { get; set; }

    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastActivity { get; set; }

    public virtual AppUser? AppUser { get; set; }
}