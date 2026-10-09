using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace IplStore.Infrastructure.Data.Configurations
{
    public class RazorpaySettings
    {
        public const string SectionName = "Razorpay";

        [Required(ErrorMessage = "Razorpay:KeyId is required.")]
        public string KeyId { get; set; } = default!;

        [Required(ErrorMessage = "Razorpay:KeySecret is required.")]
        public string KeySecret { get; set; } = default!;
    }
}
