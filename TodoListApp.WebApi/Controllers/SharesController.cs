using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoListApp.Application.Shares;
using TodoListApp.Domain.Entities;

namespace TodoListApp.WebApi.Controllers;

[ApiController]
[Route("api/lists/{listId:int}/shares")]
[Authorize]
public sealed class SharesController : ControllerBase
{
    private readonly IShareService _shares;
    public SharesController(IShareService shares) => _shares = shares;

    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // Локальні DTO (WebApi-рівень)
    public sealed record ShareDto(string UserId, string Email, string Role);
    public sealed record ShareUpsertRequest(string Email, string Role);

    /// <summary>Отримати всіх учасників списку.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ShareDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(int listId, CancellationToken ct)
    {
        var rows = await _shares.GetSharesAsync(CurrentUserId(), listId, ct);
        var dto = rows.Select(x => new ShareDto(x.UserId, x.Email, x.Role.ToString()));
        return Ok(dto);
    }

    /// <summary>Додати/оновити роль користувача за email.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Upsert(int listId, [FromBody] ShareUpsertRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req?.Email)) return BadRequest("Email is required");
        if (!Enum.TryParse<ShareRole>(req.Role, ignoreCase: true, out var role))
            return BadRequest("Role must be Reader or Writer");

        await _shares.AddOrUpdateAsync(CurrentUserId(), listId, req.Email.Trim(), role, ct);
        return NoContent();
    }

    /// <summary>Видалити доступ користувача.</summary>
    [HttpDelete("{userId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(int listId, string userId, CancellationToken ct)
    {
        await _shares.RemoveAsync(CurrentUserId(), listId, userId, ct);
        return NoContent();
    }
}
