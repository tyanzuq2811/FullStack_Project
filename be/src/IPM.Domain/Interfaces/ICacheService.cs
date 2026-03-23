namespace IPM.Domain.Interfaces;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    // Redis Sorted Set operations for Leaderboard
    Task AddToSortedSetAsync(string key, string member, double score, CancellationToken cancellationToken = default);
    Task<IEnumerable<(string member, double score)>> GetSortedSetRangeAsync(string key, int start, int stop, bool descending = true, CancellationToken cancellationToken = default);
    Task<double?> GetSortedSetScoreAsync(string key, string member, CancellationToken cancellationToken = default);
}
