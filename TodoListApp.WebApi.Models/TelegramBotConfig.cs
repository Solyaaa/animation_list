namespace TodoListApp.WebApi.Models;

public class TelegramBotConfig
{
    public string BotToken { get; set; } = string.Empty;
    public string WebhookUrl { get; set; } = string.Empty;
    public string SecretToken { get; set; } = Guid.NewGuid().ToString();
}