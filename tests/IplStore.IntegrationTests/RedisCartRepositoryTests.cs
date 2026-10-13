using IplStore.Application.Common.Exceptions;
using IplStore.Infrastructure.Repositories;
using StackExchange.Redis;

namespace IplStore.IntegrationTests;

/// <summary>
/// Needs a real Redis: docker compose -f docker-compose.infra.yml up -d redis
/// Run only these with:  dotnet test --filter-trait "Category=Redis"
/// </summary>
[Trait("Category", "Redis")]
public sealed class RedisCartRepositoryTests : IDisposable
{
    private readonly ConnectionMultiplexer _redis = ConnectionMultiplexer.Connect("localhost:6379,connectTimeout=2000");
    private readonly RedisCartRepository _repo;
    private readonly string _user = $"it-{Guid.NewGuid():N}";
    private static readonly Guid P = Guid.NewGuid();

    public RedisCartRepositoryTests() => _repo = new RedisCartRepository(_redis);

    public void Dispose()
    {
        _redis.GetDatabase().KeyDelete($"cart:{_user}");
        _redis.Dispose();
    }

    [Fact]
    public async Task Add_get_remove_round_trip()
    {
        Assert.Equal(2, await _repo.AddAsync(_user, P, 2, 10));
        Assert.Equal(5, await _repo.AddAsync(_user, P, 3, 10));
        Assert.Equal(5, (await _repo.GetAsync(_user)).Single().Quantity);

        await _repo.RemoveAsync(_user, P);
        Assert.Empty(await _repo.GetAsync(_user));
    }

    [Fact]
    public async Task Fifty_concurrent_adds_against_a_cap_of_ten_let_exactly_ten_through()
    {
        // The Lua script makes check + increment one atomic server-side step, so no update is lost and the cap holds.
        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => _repo.AddAsync(_user, P, 1, 10)));

        Assert.Equal(10, results.Count(r => r is not null));
        Assert.Equal(10, (await _repo.GetAsync(_user)).Single().Quantity);
    }

    [Fact]
    public async Task Carts_expire_so_abandoned_carts_do_not_pile_up()
    {
        await _repo.AddAsync(_user, P, 1, 10);

        var ttl = await _redis.GetDatabase().KeyTimeToLiveAsync($"cart:{_user}");

        Assert.NotNull(ttl);
        Assert.True(ttl > TimeSpan.FromDays(6));
    }

    [Fact]
    public async Task An_unreachable_redis_surfaces_as_DependencyUnavailable_not_a_raw_500()
    {
        var down = ConnectionMultiplexer.Connect("localhost:1,abortConnect=false,connectTimeout=200,syncTimeout=200,asyncTimeout=200");
        var repo = new RedisCartRepository(down);

        await Assert.ThrowsAsync<DependencyUnavailableException>(() => repo.GetAsync("anyone"));
        down.Dispose();
    }
}
