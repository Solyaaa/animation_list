using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TodoListApp.WebApi.Models;
using TodoListApp.WebApi.Models.Telegram;
using TodoListApp.WebApi.Services;

namespace TodoListApp.WebApi.Controllers;

[ApiController]
[Route("api/telegram")]
public class TelegramWebhookController : ControllerBase
{
private readonly ITelegramBotService _botService;
private readonly TelegramBotConfig _config;
private readonly ILogger<TelegramWebhookController> _logger;


public TelegramWebhookController(
    ITelegramBotService botService,
    IOptions<TelegramBotConfig> config,
    ILogger<TelegramWebhookController> logger)
{
    _botService = botService;
    _config = config.Value;
    _logger = logger;
}

[HttpPost("webhook")]
public async Task<IActionResult> HandleWebhook([FromBody] TelegramUpdate update)
{
    try
    {
        // Перевірка секретного токена (запобігає несанкціонованим викликам)
        if (Request.Headers["X-Telegram-Bot-Api-Secret-Token"] != _config.SecretToken)
        {
            _logger.LogWarning("Invalid secret token received");
            return Unauthorized();
        }

        if (update.Message?.Text == null)
            return Ok();

        var chatId = update.Message.Chat.Id;
        var messageText = update.Message.Text;
        var username = update.Message.From?.Username;

        _logger.LogInformation("Received message from {ChatId}: {Text}", chatId, messageText);

        // Обробка повідомлення
        var response = await _botService.ProcessMessageAsync(chatId, messageText);

        if (!string.IsNullOrEmpty(response))
        {
            await SendTelegramMessageAsync(chatId, response);
        }

        return Ok();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error handling Telegram webhook");
        return Ok(); // Все одно повертаємо 200, щоб Telegram не повторював
    }
}

[HttpPost("link")]
public async Task<IActionResult> LinkAccount([FromBody] LinkTelegramRequest request)
{
    try
    {
        var success = await _botService.LinkUserAsync(request);

        if (success)
        {
            await SendTelegramMessageAsync(request.TelegramUserId,
                "✅ Ваш акаунт успішно зв'язано!\n\n" +
                "Тепер ви можете використовувати бота для керування завданнями.\n" +
                "Використайте /help для списку команд.");

            return Ok(new { success = true, message = "Account linked successfully" });
        }
        else
        {
            return BadRequest(new { success = false, message = "Invalid credentials" });
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error linking Telegram account");
        return StatusCode(500, new { success = false, message = "Internal server error" });
    }
}

[HttpGet("setup-webhook")]
public async Task<IActionResult> SetupWebhook()
{
    try
    {
        var webhookUrl = $"{_config.WebhookUrl}/api/telegram/webhook";

        using var httpClient = new HttpClient();
        var url = $"https://api.telegram.org/bot{_config.BotToken}/setWebhook";

        var payload = new
        {
            url = webhookUrl,
            secret_token = _config.SecretToken,
            allowed_updates = new[] { "message" }
        };

        var response = await httpClient.PostAsJsonAsync(url, payload);
        var result = await response.Content.ReadAsStringAsync();

        _logger.LogInformation("Webhook setup result: {Result}", result);

        return Ok(new { message = "Webhook configured", result });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error setting up webhook");
        return StatusCode(500, new { error = ex.Message });
    }
}

private async Task SendTelegramMessageAsync(long chatId, string text)
{
    try
    {
        using var httpClient = new HttpClient();
        var url = $"https://api.telegram.org/bot{_config.BotToken}/sendMessage";

        var payload = new
        {
            chat_id = chatId,
            text = text,
            parse_mode = "Markdown"
        };

        await httpClient.PostAsJsonAsync(url, payload);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error sending Telegram message to {ChatId}", chatId);
    }
}
}

// Моделі для Telegram API
public class TelegramUpdate
{
    public long UpdateId { get; set; }
    public TelegramMessage? Message { get; set; }
}

public class TelegramMessage
{
    public long MessageId { get; set; }
    public TelegramUserInfo? From { get; set; }
    public TelegramChat Chat { get; set; } = new();
    public long Date { get; set; }
    public string? Text { get; set; }
}

public class TelegramUserInfo
{
    public long Id { get; set; }
    public bool IsBot { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string? Username { get; set; }
}

public class TelegramChat
{
    public long Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Username { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
}