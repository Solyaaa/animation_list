using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TodoListApp.Infrastructure.Persistence;

namespace TodoListApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseSqlite(cfg.GetConnectionString("Default")));

        services.AddIdentityCore<AppUser>()
            .AddEntityFrameworkStores<AppDbContext>();

        return services;
    }
}
