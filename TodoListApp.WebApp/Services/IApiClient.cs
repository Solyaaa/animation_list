using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TodoListApp.WebApp.Models;

namespace TodoListApp.WebApp.Services;

public sealed class ApiClient : IApiClient
{
    private readonly HttpClient http;
    private readonly IHttpContextAccessor accessor;
    private readonly ILogger<ApiClient> logger;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ApiClient(HttpClient http, IHttpContextAccessor accessor, ILogger<ApiClient> logger)
    {
        this.http = http ?? throw new ArgumentNullException(nameof(http));
        this.accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!this.http.DefaultRequestHeaders.Accept.Any())
            this.http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private void AttachToken()
    {
        var token = accessor.HttpContext?.Session.GetString("access_token");
        http.DefaultRequestHeaders.Authorization =
            string.IsNullOrWhiteSpace(token) ? null : new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<T?> GetJsonAsync<T>(string url, CancellationToken ct = default)
    {
        AttachToken();
        var resp = await http.GetAsync(url, ct);

        if (resp.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException($"API 401 for {url}");

        if (resp.StatusCode == HttpStatusCode.NotFound)
            return default;

        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<T>(JsonOpts, ct);
    }

    private async Task<HttpResponseMessage> SendAsync(Func<HttpClient, Task<HttpResponseMessage>> sender, CancellationToken ct = default)
    {
        AttachToken();
        var resp = await sender(http);
        if (resp.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException("API 401");
        return resp;
    }

    // ===== Auth =====
    public async Task<string?> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var resp = await SendAsync(h => h.PostAsJsonAsync("/api/Auth/login", new { email, password }, ct), ct);
        if (!resp.IsSuccessStatusCode) return null;
        var dto = await resp.Content.ReadFromJsonAsync<TokenDto>(JsonOpts, ct);
        return dto?.Token ?? dto?.Token;
    }

    public async Task<bool> RegisterAsync(string email, string password, CancellationToken ct = default)
    {
        var resp = await SendAsync(h => h.PostAsJsonAsync("/api/Auth/register", new { email, password }, ct), ct);
        return resp.IsSuccessStatusCode;
    }

    // ===== Lists =====
    public async Task<List<ListItemDto>> GetListsAsync(CancellationToken ct = default)
        => await GetJsonAsync<List<ListItemDto>>("/api/Lists", ct) ?? new List<ListItemDto>();

    public async Task CreateListAsync(CreateListDto dto, CancellationToken ct = default)
    {
        var resp = await SendAsync(h => h.PostAsJsonAsync("/api/Lists", dto, ct), ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task UpdateListAsync(int id, UpdateListDto dto, CancellationToken ct = default)
    {
        var resp = await SendAsync(h => h.PutAsJsonAsync($"/api/Lists/{id}", dto, ct), ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task DeleteListAsync(int id, CancellationToken ct = default)
    {
        var resp = await SendAsync(h => h.DeleteAsync($"/api/Lists/{id}", ct), ct);
        resp.EnsureSuccessStatusCode();
    }

    // ===== Tasks =====
    public async Task<List<TaskItemDto>> GetTasksInListAsync(int listId, CancellationToken ct = default)
        => await GetJsonAsync<List<TaskItemDto>>($"/api/lists/{listId}/tasks", ct) ?? new List<TaskItemDto>();

    public async Task<TaskItemDto?> GetTaskAsync(int taskId, CancellationToken ct = default)
        => await GetJsonAsync<TaskItemDto>($"/api/tasks/{taskId}", ct);

    // TodoListApp.WebApp/Services/ApiClient.cs

    public async Task CreateTaskAsync(int listId, CreateTaskDto dto, CancellationToken ct = default)
    {
        // 👇 Бек очікує створення задачі під /api/tasks
        var payload = new
        {
            listId,
            title = dto.Title,
            description = dto.Description,
            dueDate = dto.DueDate
        };

        var resp = await SendAsync(h => h.PostAsJsonAsync("/api/tasks", payload, ct), ct);
        resp.EnsureSuccessStatusCode();
    }


    public async Task UpdateTaskAsync(int taskId, UpdateTaskDto dto, CancellationToken ct = default)
    {
        var resp = await SendAsync(h => h.PutAsJsonAsync($"/api/tasks/{taskId}", dto, ct), ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task UpdateTaskAsync(int taskId, string title, string? description, DateTime? dueDate, int? status, CancellationToken ct = default)
    {
        var body = new { title, description, dueDate, status };
        var resp = await SendAsync(h => h.PutAsJsonAsync($"/api/tasks/{taskId}", body, ct), ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task ChangeStatusAsync(int taskId, int status, CancellationToken ct = default)
    {
        var resp = await SendAsync(h => h.PutAsJsonAsync($"/api/tasks/{taskId}/status", new { status }, ct), ct);
        resp.EnsureSuccessStatusCode();
    }

    // ===== Comments =====
    public async Task<List<CommentDto>> GetCommentsAsync(int taskId, CancellationToken ct = default)
        => await GetJsonAsync<List<CommentDto>>($"/api/tasks/{taskId}/comments", ct) ?? new List<CommentDto>();
    public async Task AddCommentAsync(int taskId, string text, CancellationToken ct = default)
    {
        var resp = await SendAsync(h => h.PostAsJsonAsync($"/api/tasks/{taskId}/comments", new { text }, ct), ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task EditCommentAsync(int taskId, int commentId, string text, CancellationToken ct = default)
    {
        // Використовуємо правильний роут з WebApi
        var resp = await SendAsync(h => h.PutAsJsonAsync($"/api/comments/{commentId}", new { text }, ct), ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task DeleteCommentAsync(int taskId, int commentId, CancellationToken ct = default)
    {
        // Використовуємо правильний роут з WebApi
        var resp = await SendAsync(h => h.DeleteAsync($"/api/comments/{commentId}", ct), ct);
        resp.EnsureSuccessStatusCode();
    }

    // ===== Tags =====
   /*
    public async Task<TagDtoPagedResponse> GetAllTagsPagedAsync(int page, int pageSize, string? query = null, CancellationToken ct = default)
    {
        var q = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(query)) q.Add($"q={Uri.EscapeDataString(query)}");
        var url = "/api/tags" + (q.Count > 0 ? "?" + string.Join("&", q) : "");
        return await GetJsonAsync<TagDtoPagedResponse>(url, ct) ?? new TagDtoPagedResponse { Items = new(), Page = page, PageSize = pageSize, Total = 0 };
    }
    */
   public async Task<TagDtoPagedResponse> GetAllTagsPagedAsync(int page, int pageSize, string? query = null, CancellationToken ct = default)
{
    try
    {
        var q = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(query)) q.Add($"q={Uri.EscapeDataString(query)}");
        var url = "/api/tags" + (q.Count > 0 ? "?" + string.Join("&", q) : "");

        Console.WriteLine($"🔍 Requesting tags from: {url}");

        AttachToken();
        var resp = await http.GetAsync(url, ct);

        if (resp.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException($"API 401 for {url}");

        if (!resp.IsSuccessStatusCode)
        {
            Console.WriteLine($"❌ API error: {resp.StatusCode}");
            return new TagDtoPagedResponse { Items = new(), Page = page, PageSize = pageSize, TotalCount = 0 };
        }

        // Дізнаємося, що саме повертає API
        var rawJson = await resp.Content.ReadAsStringAsync(ct);
        Console.WriteLine($"📥 Raw API response: {rawJson}");

        try
        {
            var result = JsonSerializer.Deserialize<TagDtoPagedResponse>(rawJson, JsonOpts);
            Console.WriteLine($"✅ Deserialized: {result?.Items?.Count} items");
            return result ?? new TagDtoPagedResponse { Items = new(), Page = page, PageSize = pageSize, TotalCount = 0 };
        }
        catch (JsonException jsonEx)
        {
            Console.WriteLine($"❌ JSON deserialize error: {jsonEx.Message}");

            // Спробуємо десереалізувати як простий список тегів
            try
            {
                var simpleList = JsonSerializer.Deserialize<List<TagDto>>(rawJson, JsonOpts);
                Console.WriteLine($"🔄 Fallback: API returned simple list with {simpleList?.Count} items");

                // Реалізуємо пагінацію на клієнті
                var skip = (page - 1) * pageSize;
                var pagedItems = simpleList?.Skip(skip).Take(pageSize).ToList() ?? new List<TagDto>();

                return new TagDtoPagedResponse
                {
                    Items = pagedItems,
                    TotalCount = simpleList?.Count ?? 0,
                    Page = page,
                    PageSize = pageSize
                };
            }
            catch
            {
                Console.WriteLine($"❌ Fallback also failed");
                throw;
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Exception in GetAllTagsPagedAsync: {ex.Message}");
        return new TagDtoPagedResponse { Items = new(), Page = page, PageSize = pageSize, TotalCount = 0 };
    }
}

    public async Task<List<TaskItemDto>> GetTasksByTagAsync(int tagId, CancellationToken ct = default)
        => await GetJsonAsync<List<TaskItemDto>>($"/api/tags/{tagId}/tasks", ct) ?? new List<TaskItemDto>();

    public async Task UpdateTagAsync(int id, string name, CancellationToken ct = default)
    {
        var resp = await SendAsync(h => h.PutAsJsonAsync($"/api/tags/{id}", new { name }, ct), ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task DeleteTagAsync(int id, CancellationToken ct = default)
    {
        var resp = await SendAsync(h => h.DeleteAsync($"/api/tags/{id}", ct), ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task AddTagAsync(int taskId, string name, CancellationToken ct = default)
    {
        var resp = await SendAsync(h => h.PostAsJsonAsync($"/api/tasks/{taskId}/tags", new { name }, ct), ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task RemoveTagAsync(int taskId, int tagId, CancellationToken ct = default)
    {
        var resp = await SendAsync(h => h.DeleteAsync($"/api/tasks/{taskId}/tags/{tagId}", ct), ct);
        resp.EnsureSuccessStatusCode();
    }

    // ===== Assigned =====
    public async Task<List<TaskItemDto>> GetAssignedAsync(int? status = null, string? sortBy = null, CancellationToken ct = default)
    {
        var qp = new List<string>();
        if (status is not null) qp.Add($"status={status}");
        if (!string.IsNullOrWhiteSpace(sortBy)) qp.Add($"sortBy={Uri.EscapeDataString(sortBy)}");
        var url = "/api/assigned" + (qp.Count > 0 ? "?" + string.Join("&", qp) : "");
        return await GetJsonAsync<List<TaskItemDto>>(url, ct) ?? new List<TaskItemDto>();
    }

    // ===== Search =====
    public async Task<List<TaskItemDto>> SearchAsync(string? title, DateTime? createdFrom, DateTime? dueTo, CancellationToken ct = default)
    {
        var qp = new List<string>();
        if (!string.IsNullOrWhiteSpace(title)) qp.Add($"title={Uri.EscapeDataString(title)}");
        if (createdFrom is not null) qp.Add($"createdFrom={createdFrom:O}");
        if (dueTo is not null) qp.Add($"dueTo={dueTo:O}");
        var url = "/api/search" + (qp.Count > 0 ? "?" + string.Join("&", qp) : "");
        return await GetJsonAsync<List<TaskItemDto>>(url, ct) ?? new List<TaskItemDto>();
    }
}
