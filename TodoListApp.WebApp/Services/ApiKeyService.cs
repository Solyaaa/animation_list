// WebApp/Services/ApiKeyService.cs
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TodoListApp.Infrastructure.Persistence;

namespace TodoListApp.WebApp.Services;

public interface IApiKeyService
{
    Task<string> GenerateApiKeyAsync(string userId);
    Task<bool> HasActiveKeyAsync(string userId);
}

public class ApiKeyService : IApiKeyService
{
    private readonly AppDbContext _dbContext;

    public ApiKeyService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> GenerateApiKeyAsync(string userId)
    {
        // Деактивуємо старі ключі
        var oldKeys = await _dbContext.ApiKeys
            .Where(k => k.AppUserId == userId && k.IsActive)
            .ToListAsync();

        foreach (var oldKey in oldKeys)
        {
            oldKey.IsActive = false;
        }

        // Генеруємо новий
        var key = GenerateKey();

        var apiKey = new ApiKey
        {
            Key = key,
            AppUserId = userId,
            Name = "Telegram Bot",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddYears(1),
            IsActive = true,
            LastUsedAt = DateTime.UtcNow,
            UsageCount = 0
        };

        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync();

        return key;
    }

    public async Task<bool> HasActiveKeyAsync(string userId)
    {
        return await _dbContext.ApiKeys
            .AnyAsync(k => k.AppUserId == userId && k.IsActive);
    }

    private static string GenerateKey()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        var result = new System.Text.StringBuilder(32);

        for (int i = 0; i < 32; i++)
        {
            result.Append(chars[random.Next(chars.Length)]);
        }

        return $"tk_{result}".ToLower();
    }
}