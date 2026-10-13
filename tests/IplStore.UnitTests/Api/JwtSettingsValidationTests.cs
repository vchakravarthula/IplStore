using System.ComponentModel.DataAnnotations;
using IplStore.Infrastructure.Options;

namespace IplStore.UnitTests.Api;

public sealed class JwtSettingsValidationTests
{
    private static bool IsValid(JwtSettings settings) =>
        Validator.TryValidateObject(settings, new ValidationContext(settings), new List<ValidationResult>(), validateAllProperties: true);

    [Fact]
    public void A_complete_configuration_is_valid()
        => Assert.True(IsValid(new JwtSettings { Secret = new string('s', 32), Issuer = "i", Audience = "a" }));

    [Fact]
    public void A_short_secret_is_rejected_so_weak_keys_cannot_ship()
        => Assert.False(IsValid(new JwtSettings { Secret = "too-short", Issuer = "i", Audience = "a" }));

    [Fact]
    public void Missing_values_are_rejected_so_the_app_fails_fast_instead_of_using_a_default_key()
    {
        Assert.False(IsValid(new JwtSettings { Issuer = "i", Audience = "a" }));
        Assert.False(IsValid(new JwtSettings { Secret = new string('s', 32), Audience = "a" }));
        Assert.False(IsValid(new JwtSettings { Secret = new string('s', 32), Issuer = "i" }));
    }
}
