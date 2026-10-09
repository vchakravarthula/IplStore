using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace IplStore.Infrastructure.Data.Configurations
{
    public static class ConfigurationExtensions
    {
        public static string GetRequiredValue(this IConfiguration configuration, string key)
        {
            var value = configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Critical configuration key '{key}' is missing or empty. Ensure it is set in user-secrets or environment variables.");
            }

            return value;
        }
    }
}
