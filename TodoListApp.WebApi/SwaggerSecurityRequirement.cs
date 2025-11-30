using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

public sealed class SwaggerSecurityRequirement : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Security ??= new List<OpenApiSecurityRequirement>();
        if (operation.Security.Count > 0) return;

        var bearerScheme = new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        };

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [bearerScheme] = Array.Empty<string>()
        });
    }
}