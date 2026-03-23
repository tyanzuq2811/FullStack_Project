using System.Text.Json;
using IPM.Domain.Interfaces;
using StackExchange.Redis;

namespace IPM.Infrastructure.Services;

public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly JsonSerializerOptions _jsonOptions;

    public RedisCacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _database = redis.GetDatabase();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var value = await _database.StringGetAsync(key);
        if (value.IsNullOrEmpty)
            return default;

        return JsonSerializer.Deserialize<T>(value!, _jsonOptions);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value, _jsonOptions);
        if (expiration.HasValue)
        {
            await _database.StringSetAsync(key, json, expiration.Value);
        }
        else
        {
            await _database.StringSetAsync(key, json);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _database.KeyDeleteAsync(key);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _database.KeyExistsAsync(key);
    }

    // Redis Sorted Set operations for Leaderboard
    public async Task AddToSortedSetAsync(string key, string member, double score, CancellationToken cancellationToken = default)
    {
        await _database.SortedSetAddAsync(key, member, score);
    }

    public async Task<IEnumerable<(string member, double score)>> GetSortedSetRangeAsync(
        string key, int start, int stop, bool descending = true, CancellationToken cancellationToken = default)
    {
        var entries = descending
            ? await _database.SortedSetRangeByRankWithScoresAsync(key, start, stop, Order.Descending)
            : await _database.SortedSetRangeByRankWithScoresAsync(key, start, stop, Order.Ascending);

        return entries.Select(e => (e.Element.ToString(), e.Score));
    }

    public async Task<double?> GetSortedSetScoreAsync(string key, string member, CancellationToken cancellationToken = default)
    {
        return await _database.SortedSetScoreAsync(key, member);
    }
}
