namespace TodoListApp.WebApp.Models;

public class LoginVm
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Error { get; set; }
}