using System.ComponentModel.DataAnnotations;

namespace IplStore.Infrastructure.Options;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    [Required(ErrorMessage = "Jwt:Secret is required (set it with dotnet user-secrets or an environment variable).")]
    [MinLength(32, ErrorMessage = "Jwt:Secret must be at least 256 bits (32 characters).")]
    public string Secret { get; set; } = default!;

    [Required(ErrorMessage = "Jwt:Issuer is required.")]
    public string Issuer { get; set; } = default!;

    [Required(ErrorMessage = "Jwt:Audience is required.")]
    public string Audience { get; set; } = default!;

    [Range(1, 1440)]
    public int ExpiryMinutes { get; set; } = 60;
}
