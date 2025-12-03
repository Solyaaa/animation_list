using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TodoListApp.Infrastructure.Persistence;

namespace TodoListApp.WebApi.Middleware;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    public ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
    {
        if (context.Request.Headers.TryGetValue("X-API-Key", out var apiKeyHeader))
        {
            var apiKey = apiKeyHeader.FirstOrDefault();

            if (!string.IsNullOrEmpty(apiKey))
            {
                // Шукаємо без врахування регістру
                var key = await dbContext.ApiKeys
                    .Include(ak => ak.AppUser)
                    .FirstOrDefaultAsync(ak => ak.Key.ToLower() == apiKey.ToLower() && ak.IsActive);

                if (key != null && key.AppUser != null)
                {
                    // Перевіряємо чи не прострочений ключ
                    if (!key.ExpiresAt.HasValue || key.ExpiresAt.Value > DateTime.UtcNow)
                    {
                        // Оновлюємо статистику
                        key.LastUsedAt = DateTime.UtcNow;
                        key.UsageCount++;
                        await dbContext.SaveChangesAsync();

                        // Створюємо ClaimsPrincipal для автентифікації
                        var claims = new[]
                        {
                            new Claim("uid", key.AppUserId),
                            new Claim(ClaimTypes.NameIdentifier, key.AppUserId),
                            new Claim("apikey", key.Id.ToString())
                        };

                        var identity = new ClaimsIdentity(claims, "ApiKey");
                        context.User = new ClaimsPrincipal(identity);

                        _logger.LogInformation("API key authenticated for user {UserId}", key.AppUserId);
                    }
                }
            }
        }

        await _next(context);
    }
}