using System.ComponentModel.DataAnnotations;

namespace TodoListApp.WebApp.Models;

public sealed class CreateListForm
{
    [Required, StringLength(200)]
    public string Title { get; set; } = "";

    [StringLength(1000)]
    public string? Description { get; set; }
}