using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace IplStore.Infrastructure.Data.Configurations
{
    public class JwtSettings
    {
        public const string SectionName = "Jwt";

        [Required(ErrorMessage = "Jwt:Secret is required.")]
        [MinLength(32, ErrorMessage = "Jwt:Secret must be at least 256 bits (32 characters) long.")]
        public string Secret { get; set; } = default!;

        [Required(ErrorMessage = "Jwt:Issuer is required.")]
        public string Issuer { get; set; } = default!;

        [Required(ErrorMessage = "Jwt:Audience is required.")]
        public string Audience { get; set; } = default!;
    }
}
