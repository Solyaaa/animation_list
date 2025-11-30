using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoListApp.WebApp.Models;
using TodoListApp.WebApp.Services;

namespace TodoListApp.WebApp.Controllers;
using TodoListApp.WebApp.Filters;

[Authorize]


public class TagsController(IApiClient api) : Controller
{

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
    {

        try
        {
            var result = await api.GetAllTagsPagedAsync(page, pageSize);
            Console.WriteLine($"✅ Tags loaded: {result.Items.Count} items, Total: {result.TotalCount}");
            return View(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERROR in Tags/Index: {ex.Message}");
            Console.WriteLine($"❌ Stack: {ex.StackTrace}");


            return View(new TagDtoPagedResponse
            {
                Items = new List<TagDto>(),
                Page = page,
                PageSize = pageSize,
                TotalCount = 0
            });
        }
    }

    // Задачі за тегом
    [HttpGet]
    public async Task<IActionResult> By(int id)
        => View(await api.GetTasksByTagAsync(id));

    [HttpGet]
    public IActionResult Edit(int id, string name) => View(new TagEditVm { Id = id, Name = name });

    [HttpPost]
    public async Task<IActionResult> Edit(TagEditVm vm, int page = 1, int pageSize = 20)
    {
        if (!string.IsNullOrWhiteSpace(vm.Name))
            await api.UpdateTagAsync(vm.Id, vm.Name);
        return RedirectToAction(nameof(Index), new { page, pageSize });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, int page = 1, int pageSize = 20)
    {
        await api.DeleteTagAsync(id);
        return RedirectToAction(nameof(Index), new { page, pageSize });
    }

}
