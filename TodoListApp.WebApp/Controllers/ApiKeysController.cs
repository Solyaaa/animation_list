// WebApp/Controllers/ApiKeysController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TodoListApp.WebApp.Controllers;

[Authorize]
public class ApiKeysController : Controller
{
    // GET /ApiKeys
    public IActionResult Index()
    {
        ViewBag.BotName = "todo_listik_bot";
        return View();
    }

    // POST /ApiKeys/Generate
    [HttpPost]
    public IActionResult Generate()
    {
        // Просто показуємо інструкцію, як генерувати через Swagger
        TempData["InfoMessage"] = "API ключі можна згенерувати через Swagger: /swagger або Postman";
        return RedirectToAction("Index");
    }
}