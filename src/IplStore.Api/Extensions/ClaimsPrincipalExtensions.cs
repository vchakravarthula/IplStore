using System.Security.Claims;

namespace IplStore.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The caller's id, taken ONLY from the validated token. Never from the route, query or body.</summary>
    public static string GetUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue("sub")
        ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("The token has no subject claim.");
}
