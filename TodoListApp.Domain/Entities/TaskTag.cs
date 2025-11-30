namespace TodoListApp.Domain.Entities;

public class TaskTag
{
    public int TodoTaskId { get; set; }
    public int TagId { get; set; }

    public TodoTask? Task { get; set; }
    public Tag? Tag { get; set; }
}