using System.Security.Claims;
using IplStore.Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace IplStore.Api.Controllers;

public sealed record TokenRequest(string Username);

/// <summary>
/// DEVELOPMENT-ONLY token issuer for the demo's two seeded users. It returns 404 in any other environment.
/// Production: this controller is replaced by Microsoft Entra External ID (OIDC); the API only validates tokens,
/// so nothing else in the system changes.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    // Roles are decided by the server from this table; the caller can never ask for one.
    private static readonly Dictionary<string, (string UserId, string Role, string Email)> SeededUsers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["customer"] = ("customer-1", "Customer", "customer@iplstore.local"),
            ["admin"] = ("admin-1", "Admin", "admin@iplstore.local")
        };

    private readonly JwtSettings _jwt;
    private readonly IWebHostEnvironment _environment;

    public AuthController(IOptions<JwtSettings> jwt, IWebHostEnvironment environment)
    {
        _jwt = jwt.Value;
        _environment = environment;
    }

    [HttpPost("token")]
    public IActionResult IssueToken([FromBody] TokenRequest request)
    {
        if (!_environment.IsDevelopment()) return NotFound();
        if (!SeededUsers.TryGetValue(request.Username ?? string.Empty, out var user)) return NotFound("Unknown demo user.");

        var descriptor = new SecurityTokenDescriptor
        {
            // Raw claim names ("sub", "role"): the JWT bearer setup uses MapInboundClaims = false.
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", user.UserId),
                new Claim("email", user.Email),
                new Claim("role", user.Role)
            }),
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            Expires = DateTime.UtcNow.AddMinutes(_jwt.ExpiryMinutes),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret)), SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return Ok(new { accessToken = token, tokenType = "Bearer", expiresInMinutes = _jwt.ExpiryMinutes, role = user.Role });
    }
}
