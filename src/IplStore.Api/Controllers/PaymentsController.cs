using IplStore.Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IplStore.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly RazorpaySettings _settings;

    public PaymentsController(IOptions<RazorpaySettings> settings) => _settings = settings.Value;

    /// <summary>
    /// The UI needs the PUBLIC key id to open the Razorpay checkout. Serving it from the API keeps one source of truth.
    /// The key SECRET never leaves the server.
    /// </summary>
    [HttpGet("config")]
    public IActionResult GetConfig() => Ok(new { keyId = _settings.KeyId });
}
