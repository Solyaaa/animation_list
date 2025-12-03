using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoListApp.Infrastructure.Persistence;
using TodoListApp.WebApi.Models;
using System.Security.Cryptography;
using System.Text;

namespace TodoListApp.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ApiKeysController : ControllerBase
{
private readonly AppDbContext _dbContext;
private readonly ILogger<ApiKeysController> _logger;

public ApiKeysController(AppDbContext dbContext, ILogger<ApiKeysController> logger)
{
    _dbContext = dbContext;
    _logger = logger;
}

private string UserId => User.FindFirst("uid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

// GET /api/apikeys
[HttpGet]
public async Task<ActionResult<IEnumerable<ApiKeyDto>>> GetMyKeys()
{
    var keys = await _dbContext.ApiKeys
        .Where(ak => ak.AppUserId == UserId && ak.IsActive)
        .OrderByDescending(ak => ak.CreatedAt)
        .Select(ak => new ApiKeyDto
        {
            Id = ak.Id,
            Key = $"***{ak.Key.Substring(ak.Key.Length - 8)}", // Показуємо тільки останні 8 символів
            Name = ak.Name,
            CreatedAt = ak.CreatedAt,
            ExpiresAt = ak.ExpiresAt,
            LastUsedAt = ak.LastUsedAt,
            UsageCount = ak.UsageCount
        })
        .ToListAsync();

    return Ok(keys);
}

// POST /api/apikeys
[HttpPost]
public async Task<ActionResult<ApiKeyDto>> CreateKey([FromBody] CreateApiKeyDto dto)
{
    if (string.IsNullOrWhiteSpace(dto.Name))
        return BadRequest("Name is required");

    // Генеруємо випадковий ключ
    var key = GenerateApiKey();

    var apiKey = new ApiKey
    {
        Key = key,
        AppUserId = UserId,
        Name = dto.Name.Trim(),
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = dto.ExpiresAt,
        IsActive = true,
        LastUsedAt = DateTime.UtcNow,
        UsageCount = 0
    };

    _dbContext.ApiKeys.Add(apiKey);
    await _dbContext.SaveChangesAsync();

    // Повертаємо повний ключ тільки один раз при створенні
    var result = new ApiKeyDto
    {
        Id = apiKey.Id,
        Key = key, // Повний ключ
        Name = apiKey.Name,
        CreatedAt = apiKey.CreatedAt,
        ExpiresAt = apiKey.ExpiresAt,
        LastUsedAt = apiKey.LastUsedAt,
        UsageCount = apiKey.UsageCount
    };

    return CreatedAtAction(nameof(GetMyKeys), result);
}

// DELETE /api/apikeys/{id}
[HttpDelete("{id}")]
public async Task<IActionResult> DeleteKey(int id)
{
    var apiKey = await _dbContext.ApiKeys
        .FirstOrDefaultAsync(ak => ak.Id == id && ak.AppUserId == UserId);

    if (apiKey == null)
        return NotFound();

    _dbContext.ApiKeys.Remove(apiKey);
    await _dbContext.SaveChangesAsync();

    return NoContent();
}

private static string GenerateApiKey()
{
    const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    var random = new Random();
    var result = new StringBuilder(32);

    for (int i = 0; i < 32; i++)
    {
        result.Append(chars[random.Next(chars.Length)]);
    }

    return $"tk_{result}".ToLower();
}
}