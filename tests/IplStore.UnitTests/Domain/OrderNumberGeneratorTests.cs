using IplStore.Domain.Common;

namespace IplStore.UnitTests.Domain;

public class OrderNumberGeneratorTests
{
    [Fact]
    public void Format_is_prefix_plus_ten_crockford_characters()
        => Assert.Matches("^IPL-[0-9A-HJKMNP-TV-Z]{10}$", OrderNumberGenerator.From(Guid.CreateVersion7()));

    [Fact]
    public void Same_id_always_gives_same_number()
    {
        var id = Guid.CreateVersion7();
        Assert.Equal(OrderNumberGenerator.From(id), OrderNumberGenerator.From(id));
    }

    [Fact]
    public void Generating_100k_numbers_in_a_tight_loop_produces_no_duplicates()
    {
        // The previous time-bucket + 10-bit random design produced ~27% duplicates in this exact loop.
        var seen = new HashSet<string>();
        for (var i = 0; i < 100_000; i++)
            Assert.True(seen.Add(OrderNumberGenerator.From(Guid.CreateVersion7())), $"duplicate at iteration {i}");
    }
}
