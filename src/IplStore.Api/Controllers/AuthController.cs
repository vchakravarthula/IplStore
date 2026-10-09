using IplStore.Infrastructure.Data.Configurations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace IplStore.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly JwtSettings _jwtSettings;
        private readonly IHostEnvironment _environment;

        // Hardcoded whitelist of seeded demo users allowed for local development testing
        private static readonly Dictionary<string, (string UserId, string Email, string Role)> SeededDemoUsers = new(StringComparer.OrdinalIgnoreCase)
    {
        { "admin", ("demo-admin-001", "admin@iplstore.com", "Admin") },
        { "customer", ("demo-customer-001", "customer@iplstore.com", "Customer") }
    };

        public AuthController(IOptions<JwtSettings> jwtOptions, IHostEnvironment hostEnvironment)
        {
            _jwtSettings = jwtOptions.Value;
            _environment = hostEnvironment;
        }

        [AllowAnonymous]
        [HttpPost("token")]
        public IActionResult GenerateDemoToken([FromBody] TokenRequest request)
        {
            // 1. Restrict endpoint exclusively to Development environment
            if (!_environment.IsDevelopment())
            {
                return NotFound("Demo token generation is disabled in non-development environments.");
            }

            // 2. Validate requested user against whitelisted seeded demo users
            if (string.IsNullOrWhiteSpace(request.UserId) || !SeededDemoUsers.TryGetValue(request.UserId, out var demoUser))
            {
                return BadRequest(new { Message = "Invalid demo user. Allowed users: 'admin', 'customer'." });
            }

            // 3. Mint JWT using strictly white-listed identity claims
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.Secret);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                new Claim(ClaimTypes.NameIdentifier, demoUser.UserId),
                new Claim(JwtRegisteredClaimNames.Sub, demoUser.UserId),
                new Claim(JwtRegisteredClaimNames.Email, demoUser.Email),
                new Claim(ClaimTypes.Role, demoUser.Role)
            }),
                Expires = DateTime.UtcNow.AddHours(2),
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            return Ok(new
            {
                Token = tokenString,
                UserId = demoUser.UserId,
                Role = demoUser.Role,
                ExpiresInMinutes = 120
            });
        }
    }

    public record TokenRequest(string UserId, string Email, string? Role);
}
