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


            await SendLinkedSuccessPhotoAsync(telegramUserId);


            await Task.Delay(1000);


            await SendNotificationAsync(telegramUserId,
                "🎉 Вітаємо з успішною прив'язкою!\n\n" +
                "✅ Ваші можливості:\n" +
                "• Перегляд всіх завдань\n" +
                "• Створення нових завдань\n" +
                "• Нагадування та дедлайни\n" +
                "• Пошук та редагування\n\n" +
                "👇 Щоб почати:\n" +
                "• Натисніть /start для кнопок\n" +
                "• Або /help< для допомоги");


            await Task.Delay(1500);

            var buttons = new List<List<string>>
            {
                new List<string> { "Всі завдання", "На сьогодні" },
                new List<string> { "Прострочені", "Майбутні" },
                new List<string> { "Нове завдання", "Мої списки" },
                new List<string> { "Знайти за ID", "Нагадування" }
            };

            await SendMessageWithButtonsAsync(telegramUserId,
                "🚀 Ваш TodoList готовий до роботи!\n\n" +
                "Оберіть дію з кнопок нижче:",
                buttons);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking with API key");
            return false;
        }
    }

    private async Task SendWelcomePhotoAndInstructionsAsync(long telegramUserId)
    {
        try
        {

            await SendWelcomePhotoAsync(telegramUserId);


            await Task.Delay(1000);


            await SendNotificationAsync(telegramUserId,
                "✅ <b>Акаунт успішно зв'язано!</b>\n\n" +
                "Натисніть /start для появи кнопок або /help для допомоги.");


            await Task.Delay(1500);


            var buttons = new List<List<string>>
            {
                new List<string> { "Всі", "Сьогодні", "Прострочені", "Майбутні" },
                new List<string> { "Нове завдання", "Мої списки" },
                new List<string> { "Знайти за ID", "Мої нагадування" },
                new List<string> { "/start", "/help", "/commands" }
            };

            await SendMessageWithButtonsAsync(telegramUserId,
                "🎯 Готово до роботи!\n\n" +
                "Оберіть дію з кнопок нижче або використовуйте команди:",
                buttons);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending welcome instructions to {UserId}", telegramUserId);


            await SendNotificationAsync(telegramUserId,
                "✅ Акаунт успішно зв'язано!\n\n" +
                "Натисніть /start для появи кнопок або /help для допомоги.");
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


        if (message.Trim().ToLower().StartsWith("/link") ||
            message.Trim().ToLower().StartsWith("/apikey"))
        {
            return await ProcessLinkCommand(telegramUserId, message);
        }

        if (telegramUser == null)
        {

            if (message.Trim().ToLower() == "/start")
            {

                await SendWelcomePhotoAsync(telegramUserId);
                await Task.Delay(1000);

                return "👋 Ласкаво просимо до TodoList Bot!\n\n" +
                       "Щоб почати користуватися ботом, зв'яжіть ваш акаунт:\n\n" +
                       "1. Згенеруйте API ключ у веб-версії\n" +
                       "2. Використайте команду:\n" +
                       "/link ВАШ_API_КЛЮЧ\n\n" +
                       "Або надішліть /help для інших команд.";
            }

            if (message.Trim().ToLower() == "/clear" ||
                message.Trim().ToLower() == "/reset")
            {
                return "❌ У вас ще немає прив'язаного акаунту.\n" +
                       "Спочатку зв'яжіть акаунт командою /link API_KEY";
            }

            return "Будь ласка, спочатку зв'яжіть ваш акаунт. Використайте команду /link YOUR_API_KEY\n\n" +
                   "Або надішліть /start для вітання.";
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


private async Task<string?> ProcessLinkCommand(long telegramUserId, string message)
{
    try
    {
        var parts = message.Split(' ');
        if (parts.Length < 2)
        {
            return "Використовуйте: /link YOUR_API_KEY";
        }

        var apiKey = parts[1];


        string? telegramUsername = null;
        try
        {

        }
        catch { }

        var result = await LinkWithApiKey(telegramUserId, telegramUsername, apiKey);

        if (result)
        {

            return null;
        }
        else
        {
            return "❌ Невірний API ключ або ключ неактивний.\n" +
                   "Перевірте:\n" +
                   "1. Чи правильний ключ\n" +
                   "2. Чи активний ключ\n" +
                   "3. Чи не прострочений ключ";
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing /link command");
        return "Помилка при обробці команди /link";
    }
}
    private async Task<string?> ProcessCommandAsync(TelegramUser telegramUser, string command)
    {
        var parts = command.ToLower().Split(' ');
        var mainCommand = parts[0];

        switch (mainCommand)
        {
            case "/start":

                var buttons = new List<List<string>>
                {
                    new List<string> { "Всі завдання", "На сьогодні" },
                    new List<string> { "Прострочені", "Майбутні" },
                    new List<string> { "Нове завдання", "Мої списки" },
                    new List<string> { "Знайти за ID", "Мої нагадування" },
                    new List<string> { "Допомога", "Команди" }
                };

                await SendMessageWithButtonsAsync(telegramUser.TelegramUserId,
                    "📱 <b>Головне меню TodoList Bot</b>\n\n" +
                    "Оберіть дію з кнопок нижче ⬇️",
                    buttons);

                return null;

            case "/welcome":

                await SendWelcomePhotoAsync(telegramUser.TelegramUserId);
                return "👋 Вітальне фото відправлено!";

            case "/linked":

                await SendLinkedSuccessPhotoAsync(telegramUser.TelegramUserId);
                return "✅ Фото підтвердження відправлено!";
            case "/help":
                return "📱 **TodoList Telegram Bot**\n\n" +
                       "👇 **Швидкі кнопки:**\n\n" +
                       "📋 *Завдання:*\n" +
                       "`[Всі]` `[Сьогодні]` `[Прострочені]` `[Майбутні]`\n\n" +
                       "➕ *Створити:*\n" +
                       "`[Нове завдання]`\n\n" +
                       "🔍 *Пошук:*\n" +
                       "`[Знайти за ID]` `[Знайти за назвою]`\n\n" +
                       "⚡ *Швидкі дії:*\n" +
                       "`[Мої списки]` `[Мої нагадування]`\n\n" +
                       "📖 *Повна допомога:*\n" +
                       "Надішліть `/commands` для повного списку";

            case "/commands":
                return "📖 **Повний список команд:**\n\n" +
                       "📋 *Завдання:*\n" +
                       "`/tasks` - Активні завдання\n" +
                       "`/tasks today` - На сьогодні\n" +
                       "`/tasks overdue` - Прострочені\n" +
                       "`/tasks upcoming` - Майбутні\n" +
                       "`/create Назва | Опис | 2024-12-31` - Створити\n" +
                       "`/find ID` або `/find назва` - Знайти завдання\n\n" +
                       "✏️ *Редагування:*\n" +
                       "`/edit #ID title Нова назва`\n" +
                       "`/edit #ID due 2024-12-31`\n" +
                       "`/edit #ID status InProgress`\n" +
                       "`/complete #ID` - Завершити\n" +
                       "`/delete #ID` - Видалити\n\n" +
                       "🔔 *Нагадування:*\n" +
                       "`/remind #ID HH:mm` - Нагадати\n" +
                       "`/remind #ID 09:00 daily` - Щодня\n" +
                       "`/remind #ID 10:00 weekly` - Щотижня\n" +
                       "`/reminders` - Мої нагадування\n" +
                       "`/unremind ID` - Видалити\n\n" +
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
            case "/reset":
            case "/clear":
                // Видалити користувача з бази
                var userToDelete = await _dbContext.TelegramUsers
                    .FirstOrDefaultAsync(t => t.TelegramUserId == telegramUser.TelegramUserId);

                if (userToDelete != null)
                {
                    _dbContext.TelegramUsers.Remove(userToDelete);
                    await _dbContext.SaveChangesAsync();

                    return "✅ Ваш акаунт відв'язано! Надішліть /start знову для вітання.";
                }
                return "❌ Акаунт не знайдено.";

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

    private async Task SendMessageWithButtonsAsync(long chatId, string text, List<List<string>> buttonRows)
    {
        try
        {
            var url = $"https://api.telegram.org/bot{_config.BotToken}/sendMessage";

            var keyboard = new
            {
                keyboard = buttonRows.Select(row =>
                    row.Select(button => new { text = button })
                        .ToArray()
                ).ToArray(),
                resize_keyboard = true,
                one_time_keyboard = false
            };

            var payload = new
            {
                chat_id = chatId,
                text = text,
                parse_mode = "Markdown",
                reply_markup = keyboard
            };

            await _httpClient.PostAsJsonAsync(url, payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message with buttons");
        }
    }

    private async Task<string?> ProcessTextMessageAsync(TelegramUser telegramUser, string message)
{

    var normalizedMessage = message.Trim().ToLower();


    var buttonMapping = new Dictionary<string, Func<Task<string?>>>
    {

        ["всі"] = () => GetUserTasksAsync(telegramUser, "all"),
        ["всі завдання"] = () => GetUserTasksAsync(telegramUser, "all"),
        ["завдання"] = () => GetUserTasksAsync(telegramUser, "all"),

        ["сьогодні"] = () => GetUserTasksAsync(telegramUser, "today"),
        ["на сьогодні"] = () => GetUserTasksAsync(telegramUser, "today"),
        ["сьогоднішні"] = () => GetUserTasksAsync(telegramUser, "today"),

        ["прострочені"] = () => GetUserTasksAsync(telegramUser, "overdue"),
        ["прострочено"] = () => GetUserTasksAsync(telegramUser, "overdue"),

        ["майбутні"] = () => GetUserTasksAsync(telegramUser, "upcoming"),
        ["майбутні завдання"] = () => GetUserTasksAsync(telegramUser, "upcoming"),


        ["нове завдання"] = () => Task.FromResult<string?>(
            "Щоб створити завдання, використовуйте:\n" +
            "`/create Назва | Опис | 2024-12-31`\n\n" +
            "Приклад:\n" +
            "`/create Купити молоко | 2 пакети | 2025-12-05`"),


        ["знайти"] = () => Task.FromResult<string?>(
            "🔍 Пошук завдань\n\n" +
            "• За ID: `/find 15`\n" +
            "• За назвою: `/find молоко`\n" +
            "• За описом: `/find опис`"),
        ["знайти за id"] = () => Task.FromResult<string?>(
            "Введіть ID завдання:\n`/find 15`"),
        ["пошук"] = () => Task.FromResult<string?>(
            "Для пошуку використовуйте команду `/find`"),


        ["мої списки"] = () => GetUserListsAsync(telegramUser),
        ["списки"] = () => GetUserListsAsync(telegramUser),
        ["категорії"] = () => GetUserListsAsync(telegramUser),


        ["мої нагадування"] = () => ListRemindersAsync(telegramUser.TelegramUserId),
        ["нагадування"] = () => ListRemindersAsync(telegramUser.TelegramUserId),
        ["нагади"] = () => ListRemindersAsync(telegramUser.TelegramUserId),
        ["реміндери"] = () => ListRemindersAsync(telegramUser.TelegramUserId),


        ["допомога"] = () => ProcessCommandAsync(telegramUser, "/help"),
        ["help"] = () => ProcessCommandAsync(telegramUser, "/help"),
        ["доп"] = () => ProcessCommandAsync(telegramUser, "/help"),

        ["команди"] = () => ProcessCommandAsync(telegramUser, "/commands"),
        ["всі команди"] = () => ProcessCommandAsync(telegramUser, "/commands"),


        ["як прив'язати акаунт?"] = () => Task.FromResult<string?>(
            "📋 Інструкція по прив'язці:\n\n" +
            "1. Згенеруйте API ключ у веб-версії\n" +
            "2. Скопіюйте ключ\n" +
            "3. Надішліть:\n" +
            "/link ВАШ_КЛЮЧ")
    };


    if (buttonMapping.TryGetValue(normalizedMessage, out var handler))
    {
        return await handler();
    }


    if (message.Contains("|"))
    {
        return await CreateTaskFromTextAsync(telegramUser, message);
    }

    return "Не розпізнано команду. Надішліть `/help` для допомоги або виберіть одну з кнопок.";
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


        var reminderTimeUtc = reminderTime.ToUniversalTime();


        var reminder = new TelegramReminder
        {
            TelegramUserId = telegramUserId,
            TodoTaskId = taskId,
            ReminderTime = reminderTimeUtc,
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
private async Task<bool> SendWelcomePhotoAsync(long telegramUserId)
{
    try
    {

        var imagePath = "../TodoListApp.WebApp/wwwroot/images/welcome-bot.jpg";


        return await SendLocalPhoto(telegramUserId, imagePath,
            "👋 <b>WELCOME TO YOUR TO-DO LIST BOT</b>\n\nOrganize, Prioritize, Achieve.");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error sending welcome photo");
        return false;
    }
}

private async Task<bool> SendLinkedSuccessPhotoAsync(long telegramUserId)
{
    try
    {

        var imagePath = "../TodoListApp.WebApp/wwwroot/images/linked-bot.jpg";



        return await SendLocalPhoto(telegramUserId, imagePath,
            "✅ <b>АКАУНТ УСПІШНО ЗВ'ЯЗАНО!</b>\n\nПочинайте працювати з вашим TodoList!");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error sending linked success photo");
        return false;
    }
}


private async Task<bool> SendLocalPhoto(long telegramUserId, string imagePath, string caption)
{
    try
    {
        var imageBytes = await File.ReadAllBytesAsync(imagePath);

        var url = $"https://api.telegram.org/bot{_config.BotToken}/sendPhoto";

        using var form = new MultipartFormDataContent();
        using var imageContent = new ByteArrayContent(imageBytes);

        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");

        form.Add(new StringContent(telegramUserId.ToString()), "chat_id");
        form.Add(new StringContent(caption), "caption");
        form.Add(new StringContent("HTML"), "parse_mode");
        form.Add(imageContent, "photo", Path.GetFileName(imagePath));

        var response = await _httpClient.PostAsync(url, form);
        return response.IsSuccessStatusCode;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error sending local photo");
        return false;
    }
}
public async Task CheckAndSendRemindersAsync()
{
    try
    {
        var now = DateTime.UtcNow;
        var checkWindowStart = now.AddMinutes(-5); // Допуск 5 хвилин назад


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
                var taskTitle = await GetTaskTitleAsync(reminder.TelegramUserId, reminder.TodoTaskId);

                var message = $"🔔 **Нагадування!**\n\n" +
                             $"*{taskTitle}*\n" +
                             $"⏰ {reminder.ReminderTime:HH:mm}\n\n" +
                             $"ℹ️ {reminder.Message}";

                await SendNotificationAsync(reminder.TelegramUserId, message);


                reminder.IsSent = true;
                reminder.SentAt = now;

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


        var tasksResponse = await _httpClient.GetAsync("/api/tasks/my-assigned");
        if (!tasksResponse.IsSuccessStatusCode)
            return "Не вдалося отримати завдання";

        var jsonString = await tasksResponse.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var tasks = JsonSerializer.Deserialize<List<SimpleTaskDto>>(jsonString, options) ?? new();


        var foundTasks = new List<SimpleTaskDto>();

        if (int.TryParse(searchTerm, out var searchId))
        {

            foundTasks = tasks.Where(t => t.Id == searchId).ToList();
        }
        else
        {

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
