using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TodoListApp.Infrastructure;
using TodoListApp.Infrastructure.Persistence;
using TodoListApp.WebApi.Middleware;
using TodoListApp.WebApi.Models;
using TodoListApp.WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5265;https://localhost:7266");

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<TelegramBotConfig>(builder.Configuration.GetSection("TelegramBot"));
builder.Services.AddHttpClient<ITelegramBotService, TelegramBotService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddHostedService<NotificationBackgroundService>();

builder.Services.AddControllers().AddJsonOptions(o =>
{
o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// JWT
var jwt = builder.Configuration.GetSection("Jwt");
var keyBytes = Encoding.UTF8.GetBytes(jwt["Key"]!);

builder.Services.AddAuthentication(o =>
{
o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(o =>
{
o.TokenValidationParameters = new TokenValidationParameters
{
ValidateIssuer = true,
ValidateAudience = true,
ValidateLifetime = true,
ValidateIssuerSigningKey = true,
ValidIssuer = jwt["Issuer"],
ValidAudience = jwt["Audience"],
IssuerSigningKey = new SymmetricSecurityKey(keyBytes)
};
});

builder.Services.AddAuthorization();

// CORS
const string DevCors = "DevCors";
builder.Services.AddCors(options =>
{
options.AddPolicy(DevCors, policy =>
{
if (builder.Environment.IsDevelopment())
{
policy
.AllowAnyOrigin()
.AllowAnyHeader()
.AllowAnyMethod();
}
else
{
var origins = builder.Configuration
.GetSection("Cors:AllowedOrigins")
.Get<string[]>() ?? Array.Empty<string>();

        policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    }
});
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
c.SwaggerDoc("v1", new OpenApiInfo { Title = "TodoList API", Version = "v1" });
c.CustomSchemaIds(t => t.FullName!.Replace("+", "."));
var bearer = new OpenApiSecurityScheme
{
    Name = "Authorization",
    Description = "Встав свій JWT (можна без префікса 'Bearer').",
    In = ParameterLocation.Header,
    Type = SecuritySchemeType.Http,
    Scheme = "bearer",
    BearerFormat = "JWT",
    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
};

c.AddSecurityDefinition("Bearer", bearer);
c.AddSecurityRequirement(new OpenApiSecurityRequirement
{
    { bearer, Array.Empty<string>() }
});
});

var app = builder.Build();
app.UseMiddleware<ApiKeyMiddleware>();
// Спрощена авто-міграція
using (var scope = app.Services.CreateScope())
{
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🔍 Starting database migration...");

try
{
    // Просто застосовуємо міграції
    await db.Database.MigrateAsync();
    logger.LogInformation("✅ Database migrated successfully");

    // Проста перевірка таблиць
    var canConnect = db.Database.CanConnect();
    logger.LogInformation("📊 Database can connect: {CanConnect}", canConnect);

    // Перевірка конкретно TelegramUsers
    try
    {
        var telegramUsersCount = await db.TelegramUsers.CountAsync();
        logger.LogInformation("🤖 TelegramUsers table exists with {Count} records", telegramUsersCount);
    }
    catch
    {
        logger.LogWarning("🤖 TelegramUsers table doesn't exist yet");
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "❌ Database migration failed");
    throw;
}
}

// Pipeline
app.UseSwagger();
app.UseSwaggerUI(o =>
{
o.SwaggerEndpoint("/swagger/v1/swagger.json", "TodoList API v1");
o.RoutePrefix = string.Empty;
});

app.UseHttpsRedirection();
app.UseCors(DevCors);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();