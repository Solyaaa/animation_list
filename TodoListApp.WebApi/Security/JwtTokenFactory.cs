using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TodoListApp.Infrastructure.Persistence;

namespace TodoListApp.WebApi.Security;

public static class JwtTokenFactory
{
    public static string Create(AppUser user, IEnumerable<string>? roles, IConfiguration cfg, TimeSpan? lifetime = null)
    {
        var key = cfg["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key missing");
        var issuer = cfg["Jwt:Issuer"];
        var audience = cfg["Jwt:Audience"];

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        if (!string.IsNullOrWhiteSpace(user.UserName))
            claims.Add(new(ClaimTypes.Name, user.UserName!));
        if (roles != null)
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromDays(7)),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}