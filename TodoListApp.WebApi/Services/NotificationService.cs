using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TodoListApp.WebApi.Services;

namespace TodoListApp.WebApi.Services;

public interface INotificationService
{
Task CheckAndSendNotificationsAsync();
}

public class NotificationService : INotificationService
{
private readonly ITelegramBotService _telegramBotService;
private readonly ILogger<NotificationService> _logger;

public NotificationService(
    ITelegramBotService telegramBotService,
    ILogger<NotificationService> logger)
{
    _telegramBotService = telegramBotService;
    _logger = logger;
}

public async Task CheckAndSendNotificationsAsync()
{
    try
    {
        _logger.LogInformation("Starting deadline notifications check...");
        await _telegramBotService.CheckDeadlinesAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in notification service");
    }
}
}

// Фонова служба для періодичної перевірки сповіщень
// NotificationService.cs
public class NotificationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<NotificationBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);
    // Перевіряємо кожну хвилину

    public NotificationBackgroundService(
        IServiceProvider services,
        ILogger<NotificationBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification Background Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var telegramBotService = scope.ServiceProvider.GetRequiredService<ITelegramBotService>();

                // Перевіряємо дедлайни
                await notificationService.CheckAndSendNotificationsAsync();

                // Перевіряємо нагадування
                await telegramBotService.CheckAndSendRemindersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing notification background service");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Notification Background Service is stopping.");
    }
}