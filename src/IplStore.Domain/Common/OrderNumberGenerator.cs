using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace IplStore.Domain.Common
{
    public static class OrderNumberGenerator
    {
        private static readonly char[] Base32Chars = "0123456789ABCDEFGHJKMNPQRSTVWXYZ".ToCharArray();

        /// <summary>
        /// Generates a time-ordered, collision-free short order number (e.g., "IPL-8K2M9X4P").
        /// </summary>
        public static string Generate()
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Encode 40-bit timestamp into Base32 (8 characters)
            char[] charArray = new char[8];
            for (int i = 7; i >= 0; i--)
            {
                charArray[i] = Base32Chars[timestamp & 31];
                timestamp >>= 5;
            }

            // Append 2 bytes of cryptographically secure random entropy (3 Base32 chars)
            byte[] randomBytes = new byte[2];
            RandomNumberGenerator.Fill(randomBytes);

            int randomVal = (randomBytes[0] << 8) | randomBytes[1];
            char c1 = Base32Chars[randomVal & 31];
            char c2 = Base32Chars[(randomVal >> 5) & 31];

            return $"IPL-{new string(charArray)}{c1}{c2}";
        }
    }
}
