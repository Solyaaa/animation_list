using System.ComponentModel.DataAnnotations;

namespace TodoListApp.WebApi.Models.Telegram;

public class LinkTelegramRequest
{
    [Required]
    public string ApiKey { get; set; } = string.Empty;
    [Required]
    public long TelegramUserId { get; set; }

    public string? TelegramUsername { get; set; }
}

public class TelegramLinkResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}