namespace IplStore.Domain.Common;

public static class OrderNumberGenerator
{
    // Crockford Base32: no I, L, O, U, so numbers are safe to read out over the phone.
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int RandomChars = 10; // 50 bits

    /// <summary>
    /// Derives a short, human-friendly order number from the order's own v7 GUID (50 random bits).
    /// Not mathematically collision-free (nothing short is) but the odds are negligible;
    /// the unique index on Orders.ShortOrderNumber is the backstop.
    /// </summary>
    public static string From(Guid orderId)
    {
        Span<byte> bytes = stackalloc byte[16];
        orderId.TryWriteBytes(bytes, bigEndian: true, out _);

        // v7 layout: bytes 0-5 timestamp, 6-7 version + rand_a, 8-15 variant + rand_b.
        // Use the low 7 bytes of rand_b (56 bits) and consume 50 of them.
        ulong bits = 0;
        for (var i = 9; i < 16; i++)
            bits = (bits << 8) | bytes[i];

        var chars = new char[RandomChars];
        for (var i = RandomChars - 1; i >= 0; i--)
        {
            chars[i] = Alphabet[(int)(bits & 31)];
            bits >>= 5;
        }

        return "IPL-" + new string(chars);
    }
}
