using Microsoft.AspNetCore.Authentication.Cookies;
using TodoListApp.WebApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme      = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath        = "/Auth/Login";
    options.AccessDeniedPath = "/Auth/Login";
});

builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".TodoWebApp.Session";
    options.IdleTimeout = TimeSpan.FromHours(12);
});

builder.Services.AddHttpContextAccessor();
// WebApp Program.cs

builder.Services.AddHttpClient<IApiClient, ApiClient>(client =>
{
    var baseUrl = builder.Configuration["Api:BaseUrl"];
    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException("Api:BaseUrl is not configured");
    client.BaseAddress = new Uri(baseUrl);
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Lists}/{action=Index}/{id?}");

app.Run();
