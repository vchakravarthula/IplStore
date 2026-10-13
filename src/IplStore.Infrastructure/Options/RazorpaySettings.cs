using System.ComponentModel.DataAnnotations;

namespace IplStore.Infrastructure.Options;

public sealed class RazorpaySettings
{
    public const string SectionName = "Razorpay";

    [Required(ErrorMessage = "Razorpay:KeyId is required.")]
    public string KeyId { get; set; } = default!;

    [Required(ErrorMessage = "Razorpay:KeySecret is required.")]
    public string KeySecret { get; set; } = default!;
}
