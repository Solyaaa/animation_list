using System.ComponentModel.DataAnnotations;

namespace TodoListApp.WebApi.Models.Shares;

public sealed class AddOrUpdateShareRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = default!;


    [Required]
    public string Role { get; set; } = default!;
}