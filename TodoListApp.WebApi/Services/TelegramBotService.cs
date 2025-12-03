using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TodoListApp.Infrastructure.Persistence;
using TodoListApp.WebApi.Models;
using TodoListApp.WebApi.Models.Telegram;
using TodoListApp.WebApi.Models.Tasks;
using TodoListApp.WebApi.Models.Lists;
using TaskStatus = TodoListApp.Domain.Entities.TaskStatus;

namespace TodoListApp.WebApi.Services;

public interface ITelegramBotService
{
    Task<bool> LinkUserAsync(LinkTelegramRequest request);
    Task<string?> ProcessMessageAsync(long telegramUserId, string message);
    Task SendNotificationAsync(long telegramUserId, string message);
    Task CheckDeadlinesAsync();

    // Нові методи для нагадувань
    Task<string?> SetReminderAsync(long telegramUserId, int taskId, DateTime reminderTime, string repeatInterval = "none");
    Task<string?> ListRemindersAsync(long telegramUserId);
    Task<string?> DeleteReminderAsync(long telegramUserId, int reminderId);
    Task CheckAndSendRemindersAsync();
}

public class TelegramBotService : ITelegramBotService
{
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _dbContext;
    private readonly TelegramBotConfig _config;
    private readonly ILogger<TelegramBotService> _logger;

    public TelegramBotService(
        HttpClient httpClient,
        AppDbContext dbContext,
        IOptions<TelegramBotConfig> config,
        ILogger<TelegramBotService> logger)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _config = config.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri("https://localhost:7266");
    }

    public async Task<bool> LinkUserAsync(LinkTelegramRequest request)
    {
        try
        {
            _logger.LogInformation("Looking for API key: {ApiKey}", request.ApiKey);

            var apiKey = await _dbContext.ApiKeys
                .Include(ak => ak.AppUser)
                .FirstOrDefaultAsync(ak => ak.Key.ToLower() == request.ApiKey.ToLower() && ak.IsActive);

            if (apiKey == null || apiKey.AppUser == null)
            {
                _logger.LogWarning("API key not found or inactive: {ApiKey}", request.ApiKey);
                return false;
            }

            _logger.LogInformation("Found API key: {StoredKey} for user {UserId}", apiKey.Key, apiKey.AppUserId);

            if (apiKey.ExpiresAt.HasValue && apiKey.ExpiresAt.Value < DateTime.UtcNow)
            {
                _logger.LogWarning("API key expired: {ApiKey}", request.ApiKey);
                return false;
            }

            var existingLink = await _dbContext.TelegramUsers
                .FirstOrDefaultAsync(t => t.TelegramUserId == request.TelegramUserId);

            if (existingLink != null)
            {
                _dbContext.TelegramUsers.Remove(existingLink);
            }

            apiKey.LastUsedAt = DateTime.UtcNow;
            apiKey.UsageCount++;

            var telegramUser = new TelegramUser
            {
                TelegramUserId = request.TelegramUserId,
                TelegramUsername = request.TelegramUsername,
                AppUserId = apiKey.AppUserId,
                ApiKeyId = apiKey.Id,
                LinkedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow
            };

            _dbContext.TelegramUsers.Add(telegramUser);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Successfully linked Telegram user {TelegramUserId} with API key {ApiKeyId}",
                request.TelegramUserId, apiKey.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking Telegram user");
            return false;
        }
    }

    private async Task<bool> LinkWithApiKey(long telegramUserId, string telegramUsername, string apiKey)
    {
        try
        {
            _logger.LogInformation("Linking with API key: {ApiKey}", apiKey);

            var existingLink = await _dbContext.TelegramUsers
                .FirstOrDefaultAsync(t => t.TelegramUserId == telegramUserId);

            if (existingLink != null)
            {
                _dbContext.TelegramUsers.Remove(existingLink);
            }

            var key = await _dbContext.ApiKeys
                .Include(ak => ak.AppUser)
                .FirstOrDefaultAsync(ak => ak.Key.ToLower() == apiKey.ToLower() && ak.IsActive);

            if (key == null || key.AppUser == null)
            {
                _logger.LogWarning("API key not found: {ApiKey}", apiKey);
                return false;
            }

            _logger.LogInformation("Found API key: {StoredKey}", key.Key);

            if (key.ExpiresAt.HasValue && key.ExpiresAt.Value < DateTime.UtcNow)
            {
                _logger.LogWarning("API key expired: {ApiKey}", apiKey);
                return false;
            }

            key.LastUsedAt = DateTime.UtcNow;
            key.UsageCount++;

            var telegramUser = new TelegramUser
            {
                TelegramUserId = telegramUserId,
                TelegramUsername = telegramUsername,
                AppUserId = key.AppUserId,
                ApiKeyId = key.Id,
                LinkedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow
            };

            _dbContext.TelegramUsers.Add(telegramUser);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Successfully linked Telegram user {TelegramUserId} via command", telegramUserId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking with API key");
            return false;
        }
    }

    public async Task<string?> ProcessMessageAsync(long telegramUserId, string message)
    {
        try
        {
            _logger.LogInformation("Processing message from {UserId}: {Message}", telegramUserId, message);

            var telegramUser = await _dbContext.TelegramUsers
                .Include(t => t.AppUser)
                .Include(t => t.ApiKey)
                .FirstOrDefaultAsync(t => t.TelegramUserId == telegramUserId);

            if (telegramUser == null)
            {
                return "Будь ласка, спочатку зв'яжіть ваш акаунт. Використайте команду /apikey YOUR_API_KEY";
            }

            telegramUser.LastActivity = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            if (message.StartsWith("/"))
            {
                return await ProcessCommandAsync(telegramUser, message);
            }

            return await ProcessTextMessageAsync(telegramUser, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message from {UserId}", telegramUserId);
            return "Сталася помилка. Спробуйте ще раз.";
        }
    }

    private async Task<string?> ProcessCommandAsync(TelegramUser telegramUser, string command)
    {
        var parts = command.ToLower().Split(' ');
        var mainCommand = parts[0];

        switch (mainCommand)
        {
            case "/start":
                return "Ласкаво просимо до TodoList Bot! 🎯\n\n" +
                       "Доступні команди:\n" +
                       "/link KEY - Зв'язати з акаунтом\n" +
                       "/tasks - Активні завдання\n" +
                       "/tasks today - Завдання на сьогодні\n" +
                       "/tasks overdue - Прострочені завдання\n" +
                       "/tasks upcoming - Майбутні завдання\n" +
                       "/create Назва | Опис | 2024-12-31 - Створити завдання\n" +
                       "/edit ID [title|desc|due|status] VALUE - Редагувати\n" +
                       "/delete ID - Видалити завдання\n" +
                       "/complete ID - Завершити завдання\n" +
                       "/lists - Мої списки\n" +
                       "/help - Допомога";

            case "/help":
                return "📖 **Доступні команди:**\n\n" +
                       "📋 *Завдання:*\n" +
                       "`/tasks` - Активні завдання\n" +
                       "`/tasks today` - На сьогодні\n" +
                       "`/tasks overdue` - Прострочені\n" +
                       "`/tasks upcoming` - Майбутні\n" +
                       "`/create Назва | Опис | 2024-12-31` - Створити\n" +
                       "`/find ID/назва` - Знайти завдання\n\n" +
                       "✏️ *Редагування:*\n" +
                       "`/edit #ID title Нова назва`\n" +
                       "`/edit #ID due 2024-12-31`\n" +
                       "`/edit #ID status InProgress`\n" +
                       "`/complete #ID` - Завершити\n" +
                       "`/delete #ID` - Видалити\n\n" +
                       "🔔 *Нагадування:*\n" +
                       "`/remind #ID HH:mm` - Нагадати о 15:30\n" +
                       "`/remind #ID 09:00 daily` - Щодня\n" +
                       "`/remind #ID 10:00 weekly` - Щотижня\n" +
                       "`/reminders` - Мої нагадування\n" +
                       "`/unremind ID` - Видалити нагадування\n\n" +
                       "📁 *Списки:*\n" +
                       "`/lists` - Мої списки\n\n" +
                       "🔢 *ID* завдання вказано як `#число` в `/tasks`";

            case "/link":
            case "/apikey":
                if (parts.Length > 1)
                {
                    var apiKey = parts[1];
                    var result = await LinkWithApiKey(telegramUser.TelegramUserId, telegramUser.TelegramUsername, apiKey);
                    return result ? "✅ Акаунт успішно зв'язано!" : "❌ Невірний API ключ";
                }
                return "Використовуйте: /link YOUR_API_KEY";
            case "/time":
                return await GetTimeInfoAsync();
            case "/tomorrow":
                if (parts.Length > 1)
                {
                    var idPart = parts[1];
                    if (idPart.StartsWith("#")) idPart = idPart.Substring(1);

                    if (int.TryParse(idPart, out var taskId))
                    {
                        // Нагадування на завтра о 09:00
                        var tomorrow = DateTime.UtcNow.AddDays(1).Date;
                        var reminderTime = tomorrow.AddHours(9); // 09:00
                        return await SetReminderAsync(telegramUser.TelegramUserId, taskId, reminderTime);
                    }
                }
                return "Використовуйте: `/tomorrow #ID` для нагадування завтра о 09:00";

            case "/tasks":
                if (parts.Length > 1)
                {
                    var filter = parts[1];
                    return await GetUserTasksAsync(telegramUser, filter);
                }
                return await GetUserTasksAsync(telegramUser, "all");

            case "/create":
                if (command.Length > "/create".Length)
                {
                    var taskText = command.Substring("/create".Length).Trim();
                    return await CreateTaskFromTextAsync(telegramUser, taskText);
                }
                return "Щоб створити завдання, використовуйте формат:\n`/create Назва | Опис | 2024-12-31`";

            case "/edit":
                if (parts.Length >= 4)
                {
                    // Підтримка формату /edit #ID та /edit ID
                    var idPart = parts[1];
                    if (idPart.StartsWith("#"))
                    {
                        idPart = idPart.Substring(1);
                    }

                    if (int.TryParse(idPart, out var taskId))
                    {
                        var field = parts[2];
                        var value = string.Join(" ", parts.Skip(3));
                        return await EditTaskAsync(telegramUser, taskId, field, value);
                    }
                }
                return "Використовуйте:\n`/edit #ID title Нова назва`\n`/edit #ID due 2024-12-31`\n`/edit #ID status InProgress`\n\n🔢 ID завдання можете побачити в `/tasks`";
            case "/complete":
                if (parts.Length > 1)
                {
                    var idPart = parts[1];
                    if (idPart.StartsWith("#"))
                    {
                        idPart = idPart.Substring(1);
                    }

                    if (int.TryParse(idPart, out var completeId))
                    {
                        return await CompleteTaskAsync(telegramUser, completeId);
                    }
                }
                return "Використовуйте: `/complete #ID`\nID завдання можете побачити в `/tasks`";

            case "/delete":
                if (parts.Length > 1)
                {
                    var idPart = parts[1];
                    if (idPart.StartsWith("#"))
                    {
                        idPart = idPart.Substring(1);
                    }

                    if (int.TryParse(idPart, out var deleteId))
                    {
                        return await DeleteTaskAsync(telegramUser, deleteId);
                    }
                }
                return "Використовуйте: `/delete #ID`\nID завдання можете побачити в `/tasks`";

            case "/lists":
                return await GetUserListsAsync(telegramUser);

            case "/remind":
            case "/reminder":
                if (parts.Length >= 3)
                {
                    // Формат: /remind #ID HH:mm [repeat]
                    // Приклад: /remind #15 15:30
                    // Приклад: /remind #15 09:00 daily

                    var idPart = parts[1];
                    if (idPart.StartsWith("#")) idPart = idPart.Substring(1);

                    if (int.TryParse(idPart, out var taskId) &&
                        TimeSpan.TryParse(parts[2], out var time))
                    {
                        var repeatInterval = parts.Length > 3 ? parts[3] : "none";

                        // Створюємо дату нагадування (сьогодні + час)
                        var today = DateTime.UtcNow.Date;
                        var reminderTime = today.Add(time);

                        // Якщо час вже минув сьогодні, переносимо на завтра
                        if (reminderTime < DateTime.UtcNow)
                        {
                            reminderTime = reminderTime.AddDays(1);
                        }

                        return await SetReminderAsync(telegramUser.TelegramUserId, taskId, reminderTime, repeatInterval);
                    }
                }
                return "Використовуйте:\n`/remind #ID HH:mm`\n`/remind #ID 15:30 daily`\n`/remind #ID 09:00 weekly`";

            case "/reminders":
                return await ListRemindersAsync(telegramUser.TelegramUserId);

            case "/unremind":
                if (parts.Length > 1 && int.TryParse(parts[1], out var reminderId))
                {
                    return await DeleteReminderAsync(telegramUser.TelegramUserId, reminderId);
                }
                return "Використовуйте: `/unremind ID`\nID нагадування можете побачити в `/reminders`";
            case "/find":
                if (parts.Length > 1)
                {
                    var searchTerm = string.Join(" ", parts.Skip(1));
                    return await FindTaskAsync(telegramUser, searchTerm);
                }
                return "Використовуйте: `/find ID` або `/find назва`";

            default:
                return "Невідома команда. Використайте /help для списку команд.";
        }
    }
    public async Task<string?> GetTimeInfoAsync()
    {
        try
        {
            var info = new StringBuilder();
            info.AppendLine("🕐 **Інформація про час:**");
            info.AppendLine($"UTC час: {DateTime.UtcNow:HH:mm}");
            info.AppendLine($"Локальний час: {DateTime.Now:HH:mm}");
            info.AppendLine($"Різниця: {TimeZoneInfo.Local.BaseUtcOffset.Hours} годин");

            // Перевірте нагадування
            var reminders = await _dbContext.TelegramReminders
                .Where(r => !r.IsSent)
                .OrderBy(r => r.ReminderTime)
                .ToListAsync();

            info.AppendLine($"\n📋 **Нагадування в базі:**");
            foreach (var reminder in reminders.Take(5))
            {
                info.AppendLine($"#{reminder.Id} - {reminder.ReminderTime:HH:mm} (UTC)");
            }

            return info.ToString();
        }
        catch (Exception ex)
        {
            return $"Помилка: {ex.Message}";
        }
    }
public async Task<string?> SetReminderAsync(long telegramUserId, int taskId, DateTime reminderTime, string repeatInterval = "none")
{
    try
    {
        var telegramUser = await _dbContext.TelegramUsers
            .Include(t => t.ApiKey)
            .FirstOrDefaultAsync(t => t.TelegramUserId == telegramUserId);

        if (telegramUser == null || telegramUser.ApiKey == null)
            return "Помилка: Користувач не знайдений";

        // Перевіряємо завдання
        _httpClient.DefaultRequestHeaders.Remove("X-API-Key");
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", telegramUser.ApiKey.Key);

        var taskResponse = await _httpClient.GetAsync($"/api/tasks/{taskId}");
        if (!taskResponse.IsSuccessStatusCode)
            return "Завдання не знайдено";

        var jsonString = await taskResponse.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var task = JsonSerializer.Deserialize<TaskItemDto>(jsonString, options);

        if (task == null)
            return "Завдання не знайдено";

        // ВАЖЛИВО: конвертуємо локальний час в UTC
        // Якщо reminderTime вже в UTC, не конвертуйте
        // Якщо reminderTime в локальному часі користувача:
        var reminderTimeUtc = reminderTime.ToUniversalTime();

        // Або якщо час вказано в локальному часі сервера:
        // var reminderTimeUtc = TimeZoneInfo.ConvertTimeToUtc(reminderTime, TimeZoneInfo.Local);

        var reminder = new TelegramReminder
        {
            TelegramUserId = telegramUserId,
            TodoTaskId = taskId,
            ReminderTime = reminderTimeUtc, // ← ЗБЕРІГАЄМО UTC
            Message = $"⏰ Нагадування: {task.Title}",
            RepeatInterval = repeatInterval,
            NextReminder = repeatInterval != "none" ? CalculateNextReminder(reminderTimeUtc, repeatInterval) : null,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.TelegramReminders.Add(reminder);
        await _dbContext.SaveChangesAsync();

        var repeatText = repeatInterval != "none" ? $" (повторюється {GetRepeatText(repeatInterval)})" : "";
        var localTime = reminderTimeUtc.ToLocalTime();
        return $"✅ Нагадування встановлено на {localTime:HH:mm dd.MM.yyyy}{repeatText}\nID нагадування: #{reminder.Id}";
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error setting reminder");
        return "Помилка при встановленні нагадування";
    }
}
private DateTime CalculateNextReminder(DateTime currentTime, string repeatInterval)
{
    return repeatInterval.ToLower() switch
    {
        "daily" => currentTime.AddDays(1),
        "weekly" => currentTime.AddDays(7),
        "monthly" => currentTime.AddMonths(1),
        _ => currentTime
    };
}

private string GetRepeatText(string repeatInterval)
{
    return repeatInterval.ToLower() switch
    {
        "daily" => "щодня",
        "weekly" => "щотижня",
        "monthly" => "щомісяця",
        _ => "одноразово"
    };
}
public async Task<string?> ListRemindersAsync(long telegramUserId)
{
    try
    {
        var reminders = await _dbContext.TelegramReminders
            .Where(r => r.TelegramUserId == telegramUserId && !r.IsSent)
            .OrderBy(r => r.ReminderTime)
            .ToListAsync();

        if (!reminders.Any())
            return "📭 У вас немає активних нагадувань";

        var sb = new StringBuilder();
        sb.AppendLine("🔔 **Ваші нагадування:**");

        foreach (var reminder in reminders)
        {
            var statusIcon = reminder.IsSent ? "✅" : "⏳";
            var repeatIcon = reminder.RepeatInterval != "none" ? "🔁" : "";
            var timeText = reminder.ReminderTime.ToString("HH:mm dd.MM.yyyy");

            // Використовуємо ID завдання без API викликів
            sb.AppendLine($"\n`#{reminder.Id}` {statusIcon}{repeatIcon} Завдання #{reminder.TodoTaskId}");
            sb.AppendLine($"   🕐 {timeText}");

            if (reminder.RepeatInterval != "none")
            {
                sb.AppendLine($"   🔁 {GetRepeatText(reminder.RepeatInterval)}");
            }

            if (!string.IsNullOrEmpty(reminder.Message))
            {
                sb.AppendLine($"   📝 {reminder.Message}");
            }
        }

        sb.AppendLine($"\nℹ️ Використовуйте `/unremind ID` для видалення");

        return sb.ToString();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error listing reminders");
        return "Помилка при отриманні нагадувань";
    }
}
public async Task<string?> DeleteReminderAsync(long telegramUserId, int reminderId)
{
    try
    {
        var reminder = await _dbContext.TelegramReminders
            .FirstOrDefaultAsync(r => r.Id == reminderId && r.TelegramUserId == telegramUserId);

        if (reminder == null)
            return "Нагадування не знайдено";

        _dbContext.TelegramReminders.Remove(reminder);
        await _dbContext.SaveChangesAsync();

        return $"🗑️ Нагадування #{reminderId} видалено";
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error deleting reminder");
        return "Помилка при видаленні нагадування";
    }
}
public async Task CheckAndSendRemindersAsync()
{
    try
    {
        var now = DateTime.UtcNow;
        var checkWindowStart = now.AddMinutes(-5); // Допуск 5 хвилин назад

        // Знаходимо всі нагадування, які мали спрацювати в останні 5 хвилин
        var reminders = await _dbContext.TelegramReminders
            .Where(r => !r.IsSent &&
                        r.ReminderTime <= now &&
                        r.ReminderTime >= checkWindowStart)
            .ToListAsync();

        _logger.LogInformation("Found {Count} reminders to send (window: {WindowStart} - {Now})",
            reminders.Count, checkWindowStart.ToString("HH:mm"), now.ToString("HH:mm"));

        foreach (var reminder in reminders)
        {
            try
            {
                // Отримуємо інформацію про завдання через API
                var taskTitle = await GetTaskTitleAsync(reminder.TelegramUserId, reminder.TodoTaskId);

                var message = $"🔔 **Нагадування!**\n\n" +
                             $"*{taskTitle}*\n" +
                             $"⏰ {reminder.ReminderTime:HH:mm}\n\n" +
                             $"ℹ️ {reminder.Message}";

                await SendNotificationAsync(reminder.TelegramUserId, message);

                // Оновлюємо статус
                reminder.IsSent = true;
                reminder.SentAt = now;

                // Якщо повторюване нагадування
                if (reminder.RepeatInterval != "none" && reminder.NextReminder.HasValue)
                {
                    var nextReminder = new TelegramReminder
                    {
                        TelegramUserId = reminder.TelegramUserId,
                        TodoTaskId = reminder.TodoTaskId,
                        ReminderTime = reminder.NextReminder.Value,
                        Message = reminder.Message,
                        RepeatInterval = reminder.RepeatInterval,
                        NextReminder = CalculateNextReminder(reminder.NextReminder.Value, reminder.RepeatInterval),
                        CreatedAt = DateTime.UtcNow
                    };

                    _dbContext.TelegramReminders.Add(nextReminder);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending reminder {ReminderId}", reminder.Id);
            }
        }

        if (reminders.Any())
        {
            await _dbContext.SaveChangesAsync();
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error checking reminders");
    }
}

// Допоміжний метод для отримання назви завдання
private async Task<string> GetTaskTitleAsync(long telegramUserId, int taskId)
{
    try
    {
        var telegramUser = await _dbContext.TelegramUsers
            .Include(t => t.ApiKey)
            .FirstOrDefaultAsync(t => t.TelegramUserId == telegramUserId);

        if (telegramUser?.ApiKey == null)
            return "Завдання";

        _httpClient.DefaultRequestHeaders.Remove("X-API-Key");
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", telegramUser.ApiKey.Key);

        var response = await _httpClient.GetAsync($"/api/tasks/{taskId}");

        if (!response.IsSuccessStatusCode)
            return "Завдання";

        var jsonString = await response.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var task = JsonSerializer.Deserialize<TaskItemDto>(jsonString, options);

        return task?.Title ?? "Завдання";
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error getting task title for reminder");
        return "Завдання";
    }
}
private async Task<string?> FindTaskAsync(TelegramUser telegramUser, string searchTerm)
{
    try
    {
        if (telegramUser.ApiKey == null)
            return "Помилка: API ключ не знайдений";

        _httpClient.DefaultRequestHeaders.Remove("X-API-Key");
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", telegramUser.ApiKey.Key);

        // Отримуємо всі завдання
        var tasksResponse = await _httpClient.GetAsync("/api/tasks/my-assigned");
        if (!tasksResponse.IsSuccessStatusCode)
            return "Не вдалося отримати завдання";

        var jsonString = await tasksResponse.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var tasks = JsonSerializer.Deserialize<List<SimpleTaskDto>>(jsonString, options) ?? new();

        // Шукаємо за ID або назві
        var foundTasks = new List<SimpleTaskDto>();

        if (int.TryParse(searchTerm, out var searchId))
        {
            // Пошук за ID
            foundTasks = tasks.Where(t => t.Id == searchId).ToList();
        }
        else
        {
            // Пошук за назвою
            foundTasks = tasks.Where(t =>
                t.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                (t.Description != null && t.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        if (!foundTasks.Any())
        {
            return $"🔍 Не знайдено завдань за запитом: '{searchTerm}'";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"🔍 **Знайдено завдань:** {foundTasks.Count}");

        var today = DateTime.UtcNow.Date;

        foreach (var task in foundTasks)
        {
            var statusIcon = task.Status?.ToLower() switch
            {
                "pending" => "⏳",
                "inprogress" => "🔄",
                "done" => "✅",
                _ => "📝"
            };

            var isOverdue = task.DueDate.HasValue &&
                           task.DueDate.Value.Date < today &&
                           task.Status?.ToLower() != "done";

            var overdueIcon = isOverdue ? "🚨 " : "";
            var dueText = task.DueDate.HasValue ?
                $"\n   📅 {task.DueDate.Value:dd.MM.yyyy}" : "";

            sb.AppendLine($"\n`#{task.Id}` {overdueIcon}{statusIcon} *{task.Title}*{dueText}");

            if (!string.IsNullOrEmpty(task.Description))
            {
                sb.AppendLine($"   📝 {task.Description}");
            }

            sb.AppendLine($"   📋 Команди:");
            sb.AppendLine($"      `/edit #{task.Id} title Нова назва`");
            sb.AppendLine($"      `/complete #{task.Id}`");
            sb.AppendLine($"      `/delete #{task.Id}`");
        }

        telegramUser.ApiKey.LastUsedAt = DateTime.UtcNow;
        telegramUser.ApiKey.UsageCount++;
        await _dbContext.SaveChangesAsync();

        return sb.ToString();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error finding task with term: {Term}", searchTerm);
        return "Помилка при пошуку завдання";
    }
}
    private async Task<string?> GetUserTasksAsync(TelegramUser telegramUser, string filter = "all")
{
    try
    {
        if (telegramUser.ApiKey == null)
            return "Помилка: API ключ не знайдений";

        _httpClient.DefaultRequestHeaders.Remove("X-API-Key");
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", telegramUser.ApiKey.Key);

        // ЗАВЖДИ запитуємо всі завдання
        var tasksResponse = await _httpClient.GetAsync("/api/tasks/my-assigned");

        if (!tasksResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to get tasks: {StatusCode}", tasksResponse.StatusCode);
            return "Не вдалося отримати завдання";
        }

        var jsonString = await tasksResponse.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        List<SimpleTaskDto> allTasks;
        try
        {
            allTasks = JsonSerializer.Deserialize<List<SimpleTaskDto>>(jsonString, options) ?? new();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize tasks");
            return "Помилка при обробці даних";
        }

        // Фільтрація на стороні бота
        var today = DateTime.UtcNow.Date;
        List<SimpleTaskDto> filteredTasks = filter.ToLower() switch
        {
            "today" => allTasks.Where(t =>
                t.DueDate.HasValue &&
                t.DueDate.Value.Date == today &&
                t.Status?.ToLower() != "done").ToList(),

            "overdue" => allTasks.Where(t =>
                t.DueDate.HasValue &&
                t.DueDate.Value.Date < today &&
                t.Status?.ToLower() != "done").ToList(),

            "upcoming" => allTasks.Where(t =>
                t.DueDate.HasValue &&
                t.DueDate.Value.Date > today &&
                t.Status?.ToLower() != "done").ToList(),

            _ => allTasks.Where(t => t.Status?.ToLower() != "done").ToList() // "all"
        };

        if (!filteredTasks.Any())
        {
            return filter switch
            {
                "today" => "📭 У вас немає завдань на сьогодні",
                "overdue" => "📭 У вас немає прострочених завдань",
                "upcoming" => "📭 У вас немає майбутніх завдань",
                _ => "📭 У вас немає активних завдань"
            };
        }

        var sb = new StringBuilder();

        switch (filter)
        {
            case "today":
                sb.AppendLine($"📅 **Завдання на сьогодні** ({filteredTasks.Count})");
                break;
            case "overdue":
                sb.AppendLine($"🚨 **Прострочені завдання** ({filteredTasks.Count})");
                break;
            case "upcoming":
                sb.AppendLine($"🔮 **Майбутні завдання** ({filteredTasks.Count})");
                break;
            default:
                sb.AppendLine($"📋 **Всі активні завдання** ({filteredTasks.Count})");
                break;
        }

        // Сортування за датою
        filteredTasks = filteredTasks
            .OrderBy(t => t.DueDate ?? DateTime.MaxValue)
            .ToList();

        foreach (var task in filteredTasks.Take(15))
        {
            var statusIcon = task.Status?.ToLower() switch
            {
                "pending" => "⏳",
                "inprogress" => "🔄",
                "done" => "✅",
                _ => "📝"
            };

            var isOverdue = task.DueDate.HasValue &&
                           task.DueDate.Value.Date < today &&
                           task.Status?.ToLower() != "done";

            var overdueIcon = isOverdue ? "🚨 " : "";
            var dueText = task.DueDate.HasValue ?
                $"\n   📅 {task.DueDate.Value:dd.MM.yyyy}" : "";

            sb.AppendLine($"`#{task.Id}` {overdueIcon}{statusIcon} *{task.Title}*{dueText}");

            if (!string.IsNullOrEmpty(task.Description))
            {
                var shortDescription = task.Description.Length > 50
                    ? task.Description.Substring(0, 47) + "..."
                    : task.Description;
                sb.AppendLine($"   📝 {shortDescription}");
            }

            sb.AppendLine();
        }

        if (filteredTasks.Count > 15)
            sb.AppendLine($"\n... і ще {filteredTasks.Count - 15} завдань");

        telegramUser.ApiKey.LastUsedAt = DateTime.UtcNow;
        telegramUser.ApiKey.UsageCount++;
        await _dbContext.SaveChangesAsync();
        sb.AppendLine($"\nℹ️ *ID* завдання вказано як `#число`");
        sb.AppendLine($"Використовуйте `/edit #ID` для редагування");

        return sb.ToString();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error getting user tasks");
        return "Помилка при отриманні завдань";
    }
}

    private async Task<string?> ProcessTextMessageAsync(TelegramUser telegramUser, string message)
    {
        if (message.Contains("|"))
        {
            return await CreateTaskFromTextAsync(telegramUser, message);
        }

        return "Не розпізнано команду. Використайте /help для допомоги.";
    }

    private async Task<string?> CreateTaskFromTextAsync(TelegramUser telegramUser, string text)
    {
        try
        {
            var parts = text.Split('|', 3);
            if (parts.Length < 1) return "Невірний формат";

            var title = parts[0].Trim();
            var description = parts.Length > 1 ? parts[1].Trim() : null;
            DateTime? dueDate = null;

            if (parts.Length > 2 && DateTime.TryParse(parts[2].Trim(), out var parsedDate))
            {
                dueDate = parsedDate;
            }

            if (telegramUser.ApiKey == null)
                return "Помилка: API ключ не знайдений";

            _httpClient.DefaultRequestHeaders.Remove("X-API-Key");
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", telegramUser.ApiKey.Key);

            var listsResponse = await _httpClient.GetAsync("/api/lists");
            if (!listsResponse.IsSuccessStatusCode)
                return "Не вдалося отримати списки";

            var lists = await listsResponse.Content.ReadFromJsonAsync<List<ListItemDto>>();
            var firstList = lists?.FirstOrDefault();

            if (firstList == null)
                return "Спочатку створіть список у веб-версії";

            var createTaskDto = new
            {
                Title = title,
                Description = description,
                DueDate = dueDate,
                ListId = firstList.Id
            };

            var response = await _httpClient.PostAsJsonAsync("/api/tasks", createTaskDto);

            if (response.IsSuccessStatusCode)
            {
                telegramUser.ApiKey.LastUsedAt = DateTime.UtcNow;
                telegramUser.ApiKey.UsageCount++;
                await _dbContext.SaveChangesAsync();

                var dueText = dueDate.HasValue ? $" до {dueDate.Value:dd.MM.yyyy}" : "";
                return $"✅ Завдання \"{title}\" створено{dueText}!";
            }
            else
            {
                _logger.LogError("Failed to create task: {StatusCode}", response.StatusCode);
                return "❌ Не вдалося створити завдання";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating task from text");
            return "Помилка при створенні завдання";
        }
    }

    private async Task<string?> EditTaskAsync(TelegramUser telegramUser, int taskId, string field, string value)
    {
        try
        {
            if (telegramUser.ApiKey == null)
                return "Помилка: API ключ не знайдений";

            _httpClient.DefaultRequestHeaders.Remove("X-API-Key");
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", telegramUser.ApiKey.Key);

            var updateDto = new Dictionary<string, object>();

            switch (field.ToLower())
            {
                case "title":
                    updateDto["Title"] = value;
                    break;
                case "desc":
                case "description":
                    updateDto["Description"] = value;
                    break;
                case "due":
                case "duedate":
                    if (DateTime.TryParse(value, out var dueDate))
                        updateDto["DueDate"] = dueDate;
                    else
                        return "Невірний формат дати. Використовуйте YYYY-MM-DD";
                    break;
                case "status":
                    updateDto["Status"] = value;
                    break;
                default:
                    return $"Невідоме поле: {field}";
            }

            var response = await _httpClient.PutAsJsonAsync($"/api/tasks/{taskId}", updateDto);

            if (response.IsSuccessStatusCode)
            {
                telegramUser.ApiKey.LastUsedAt = DateTime.UtcNow;
                telegramUser.ApiKey.UsageCount++;
                await _dbContext.SaveChangesAsync();
                return $"✅ Завдання #{taskId} оновлено!";
            }
            else
            {
                _logger.LogError("Failed to update task: {StatusCode}", response.StatusCode);
                return "❌ Не вдалося оновити завдання";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing task");
            return "Помилка при редагуванні завдання";
        }
    }

    private async Task<string?> CompleteTaskAsync(TelegramUser telegramUser, int taskId)
    {
        try
        {
            if (telegramUser.ApiKey == null)
                return "Помилка: API ключ не знайдений";

            _httpClient.DefaultRequestHeaders.Remove("X-API-Key");
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", telegramUser.ApiKey.Key);

            var updateDto = new { Status = "Done" };

            var response = await _httpClient.PutAsJsonAsync($"/api/tasks/{taskId}", updateDto);

            if (response.IsSuccessStatusCode)
            {
                telegramUser.ApiKey.LastUsedAt = DateTime.UtcNow;
                telegramUser.ApiKey.UsageCount++;
                await _dbContext.SaveChangesAsync();
                return $"✅ Завдання #{taskId} завершено!";
            }
            else
            {
                _logger.LogError("Failed to complete task: {StatusCode}", response.StatusCode);
                return "❌ Не вдалося завершити завдання";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing task");
            return "Помилка при завершенні завдання";
        }
    }

    private async Task<string?> DeleteTaskAsync(TelegramUser telegramUser, int taskId)
    {
        try
        {
            if (telegramUser.ApiKey == null)
                return "Помилка: API ключ не знайдений";

            _httpClient.DefaultRequestHeaders.Remove("X-API-Key");
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", telegramUser.ApiKey.Key);

            var response = await _httpClient.DeleteAsync($"/api/tasks/{taskId}");

            if (response.IsSuccessStatusCode)
            {
                telegramUser.ApiKey.LastUsedAt = DateTime.UtcNow;
                telegramUser.ApiKey.UsageCount++;
                await _dbContext.SaveChangesAsync();
                return $"🗑️ Завдання #{taskId} видалено!";
            }
            else
            {
                _logger.LogError("Failed to delete task: {StatusCode}", response.StatusCode);
                return "❌ Не вдалося видалити завдання";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting task");
            return "Помилка при видаленні завдання";
        }
    }

    private async Task<string?> GetUserListsAsync(TelegramUser telegramUser)
    {
        try
        {
            if (telegramUser.ApiKey == null)
                return "Помилка: API ключ не знайдений";

            _httpClient.DefaultRequestHeaders.Remove("X-API-Key");
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", telegramUser.ApiKey.Key);

            var listsResponse = await _httpClient.GetAsync("/api/lists");
            if (!listsResponse.IsSuccessStatusCode)
                return "Не вдалося отримати списки";

            var lists = await listsResponse.Content.ReadFromJsonAsync<List<ListItemDto>>();

            if (lists == null || !lists.Any())
                return "📭 У вас немає списків";

            telegramUser.ApiKey.LastUsedAt = DateTime.UtcNow;
            telegramUser.ApiKey.UsageCount++;
            await _dbContext.SaveChangesAsync();

            var sb = new StringBuilder();
            sb.AppendLine("📁 **Ваші списки:**");

            foreach (var list in lists)
            {
                sb.AppendLine($"• {list.Title} ({list.TasksCount} завдань)");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user lists");
            return "Помилка при отриманні списків";
        }
    }

    public async Task SendNotificationAsync(long telegramUserId, string message)
    {
        try
        {
            var url = $"https://api.telegram.org/bot{_config.BotToken}/sendMessage";
            var payload = new
            {
                chat_id = telegramUserId,
                text = message,
                parse_mode = "Markdown"
            };

            await _httpClient.PostAsJsonAsync(url, payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Telegram notification");
        }
    }

    public async Task CheckDeadlinesAsync()
    {
        try
        {
            _logger.LogInformation("Checking deadlines for notifications...");

            var telegramUsers = await _dbContext.TelegramUsers
                .Include(t => t.ApiKey)
                .Where(t => t.ApiKey != null && t.ApiKey.IsActive)
                .ToListAsync();

            foreach (var telegramUser in telegramUsers)
            {
                try
                {
                    _httpClient.DefaultRequestHeaders.Remove("X-API-Key");
                    _httpClient.DefaultRequestHeaders.Add("X-API-Key", telegramUser.ApiKey!.Key);

                    var tasksResponse = await _httpClient.GetAsync("/api/tasks/my-assigned");
                    if (!tasksResponse.IsSuccessStatusCode) continue;

                    var jsonString = await tasksResponse.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var tasks = JsonSerializer.Deserialize<List<SimpleTaskDto>>(jsonString, options);

                    if (tasks == null) continue;

                    var now = DateTime.UtcNow;
                    var oneHourFromNow = now.AddHours(1);

                    foreach (var task in tasks)
                    {
                        if (task.DueDate.HasValue &&
                            task.DueDate.Value > now &&
                            task.DueDate.Value <= oneHourFromNow &&
                            task.Status?.ToLower() != "done")
                        {
                            var notificationMessage = $"🚨 **Спопішення!**\n\n" +
                                                    $"Завдання \"{task.Title}\" закінчується через годину!\n" +
                                                    $"⏰ {task.DueDate.Value:HH:mm}";

                            await SendNotificationAsync(telegramUser.TelegramUserId, notificationMessage);
                        }
                    }

                    telegramUser.ApiKey.LastUsedAt = DateTime.UtcNow;
                    telegramUser.ApiKey.UsageCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking deadlines for user {UserId}", telegramUser.TelegramUserId);
                }
            }

            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking deadlines");
        }
    }
}

// Допоміжні моделі для телеграм бота
public class SimpleTaskDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Status { get; set; }
    public string AssignedUserId { get; set; } = string.Empty;
    public bool IsOverdue =>
        DueDate.HasValue &&
        DueDate.Value.Date < DateTime.UtcNow.Date &&
        Status?.ToLower() != "done";
}

// Модель без Priority для десеріалізації
public class TaskItemDtoWithoutPriority
{
    public int Id { get; set; }
    public int TodoListId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public TaskStatus Status { get; set; } // Enum
    public string AssignedUserId { get; set; } = "";

    public bool IsOverdue =>
        DueDate.HasValue &&
        DueDate.Value.Date < DateTime.UtcNow.Date &&
        Status != TaskStatus.Done;
}
