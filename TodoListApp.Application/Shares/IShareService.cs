using TodoListApp.Domain.Entities;

namespace TodoListApp.Application.Shares;

/// <summary>Сервіс спільного доступу до списків.</summary>
public interface IShareService
{
    /// <summary>Отримати всіх учасників списку. Доступно власнику або Writer.</summary>
    Task<IReadOnlyList<(string UserId, string Email, ShareRole Role)>>
        GetSharesAsync(string requesterId, int listId, CancellationToken ct);

    /// <summary>Додати або оновити роль користувача за email.</summary>
    Task AddOrUpdateAsync(string ownerId, int listId, string email, ShareRole role, CancellationToken ct);

    /// <summary>Прибрати доступ користувача.</summary>
    Task RemoveAsync(string ownerId, int listId, string userId, CancellationToken ct);
}