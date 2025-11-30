namespace TodoListApp.Domain.Entities;

public enum ShareRole
{
    Reader = 0,
    Writer = 1
}

public sealed class ListShare
{
    public int Id { get; set; }

    public int TodoListId { get; set; }
    public TodoList? TodoList { get; set; }

    public string UserId { get; set; } = default!;
    public ShareRole Role { get; set; }
}