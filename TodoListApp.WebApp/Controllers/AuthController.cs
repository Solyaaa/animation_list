using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoListApp.WebApp.Services;

namespace TodoListApp.WebApp.Controllers;

[AllowAnonymous]
public class AuthController : Controller
{
    private readonly IApiClient api;

    public AuthController(IApiClient api) => this.api = api;

    [AllowAnonymous] public IActionResult Login() => View();
    [HttpGet] public IActionResult Register() => View();

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password)
    {
        var token = await api.LoginAsync(email, password);
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["Error"] = "Invalid email or password.";
            return View();
        }

        // кладемо JWT у Session → ApiClient приклеїть його до кожного запиту
        HttpContext.Session.SetString("access_token", token);

        // піднімаємо cookie-логін для MVC
        var claims = new[] { new Claim(ClaimTypes.Name, email) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies"));
        await HttpContext.SignInAsync("Cookies", principal);

        return RedirectToAction("Index", "Lists");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string email, string password)
    {
        var ok = await api.RegisterAsync(email, password);
        if (!ok)
        {
            TempData["Error"] = "Registration failed.";
            return View();
        }
        return RedirectToAction(nameof(Login));
    }

    // В AuthController.cs
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.Session.Remove("access_token");

        return RedirectToAction("Login", "Auth");
    }
}
