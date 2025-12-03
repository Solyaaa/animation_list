using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoListApp.Domain.Entities;
using TodoListApp.Infrastructure.Persistence;
using TodoListApp.WebApi.Models.Lists;
using TodoListApp.WebApi.Models.Tasks;
using TaskStatus = TodoListApp.Domain.Entities.TaskStatus;

namespace TodoListApp.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ListsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ListsController(AppDbContext db) => _db = db;

    private string UserId => User.FindFirst("uid")?.Value ?? "";

    [HttpGet("{id:int}/tasks")]
    public async Task<ActionResult<IEnumerable<TaskItemDto>>> GetTasksInList(int id)
    {
        var tasks = await _db.TodoTasks
            .Include(t => t.TaskTags).ThenInclude(tt => tt.Tag)
            .Where(t => t.TodoListId == id)
            .Select(t => new TaskItemDto
            {
                Id = t.Id,
                TodoListId = t.TodoListId,
                Title = t.Title,
                Description = t.Description,
                DueDate = t.DueDate,
                Status = t.Status.ToString(), // ← КОНВЕРТУЄМО В STRING
                AssignedUserId = t.AssignedUserId,
                Tags = t.TaskTags
                    .Select(tt => new WebApi.Models.Tags.TagDto { Id = tt.TagId, Name = tt.Tag!.Name })
                    .ToList()
            })
            .ToListAsync();

        return Ok(tasks);
    }

    // ... інші методи залишаються без змін ...

// POST /api/lists/{id}/tasks
    // сигнатура
    [HttpPost("{id:int}/tasks")]
    public async Task<ActionResult<int>> CreateTask(int id, [FromBody] CreateTaskDto body)
    {
        var list = await _db.TodoLists.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == UserId);
        if (list is null) return NotFound();

        var task = new TodoTask
        {
            Title = body.Title,
            Description = body.Description,
            DueDate = body.DueDate,
            TodoListId = id,
            AssignedUserId = UserId,
            Status = TaskStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.TodoTasks.Add(task);
        await _db.SaveChangesAsync();
        return Created($"/api/tasks/{task.Id}", task.Id);
    }


    [HttpGet]
    public async Task<IEnumerable<ListItemDto>> GetMyLists()
    {
        return await _db.TodoLists
            .Where(l => l.OwnerId == UserId)
            .Select(l => new ListItemDto
            {
                Id = l.Id,
                Title = l.Title,
                Description = l.Description,
                CreatedAt = l.CreatedAt,
                TasksCount = l.Tasks.Count
            })
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ListItemDto>> Get(int id)
    {
        var l = await _db.TodoLists.Include(x => x.Tasks)
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == UserId);
        if (l is null) return NotFound();

        return new ListItemDto
        {
            Id = l.Id, Title = l.Title, Description = l.Description,
            CreatedAt = l.CreatedAt, TasksCount = l.Tasks.Count
        };
    }

    [HttpPost]
    public async Task<ActionResult<int>> Create([FromBody] CreateListDto dto)
    {
        var l = new TodoList { Title = dto.Title, Description = dto.Description, OwnerId = UserId };
        _db.TodoLists.Add(l);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = l.Id }, l.Id);
    }



    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateListDto dto)
    {
        var l = await _db.TodoLists.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == UserId);
        if (l is null) return NotFound();
        l.Title = dto.Title; l.Description = dto.Description;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var l = await _db.TodoLists.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == UserId);
        if (l is null) return NotFound();
        _db.TodoLists.Remove(l);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
