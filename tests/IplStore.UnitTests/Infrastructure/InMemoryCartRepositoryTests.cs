using IplStore.Infrastructure.Repositories;

namespace IplStore.UnitTests.Infrastructure;

public sealed class InMemoryCartRepositoryTests
{
    private readonly InMemoryCartRepository _repo = new();
    private static readonly Guid P = Guid.NewGuid();

    [Fact]
    public async Task Add_accumulates_and_reports_the_new_quantity()
    {
        Assert.Equal(2, await _repo.AddAsync("u", P, 2, 10));
        Assert.Equal(5, await _repo.AddAsync("u", P, 3, 10));
    }

    [Fact]
    public async Task Add_that_would_exceed_the_cap_returns_null_and_changes_nothing()
    {
        await _repo.AddAsync("u", P, 8, 10);

        Assert.Null(await _repo.AddAsync("u", P, 3, 10));
        Assert.Equal(8, (await _repo.GetAsync("u")).Single().Quantity);
    }

    [Fact]
    public async Task Fifty_concurrent_adds_against_a_cap_of_ten_let_exactly_ten_through()
    {
        // The property the Redis Lua script guarantees too: check-and-increment is one atomic step.
        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(() => _repo.AddAsync("u", P, 1, 10))));

        Assert.Equal(10, results.Count(r => r is not null));
        Assert.Equal(10, (await _repo.GetAsync("u")).Single().Quantity);
    }

    [Fact]
    public async Task Remove_and_clear_work_and_unknown_users_are_empty()
    {
        var other = Guid.NewGuid();
        await _repo.AddAsync("u", P, 1, 10);
        await _repo.AddAsync("u", other, 1, 10);

        await _repo.RemoveAsync("u", P);
        Assert.Equal(other, (await _repo.GetAsync("u")).Single().ProductId);

        await _repo.ClearAsync("u");
        Assert.Empty(await _repo.GetAsync("u"));
        Assert.Empty(await _repo.GetAsync("nobody"));
    }
}
