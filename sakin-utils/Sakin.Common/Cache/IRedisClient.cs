namespace Sakin.Common.Cache
{
    public interface IRedisClient
    {
        Task<bool> StringSetAsync(string key, string value, TimeSpan? expiry = null);
        Task<string?> StringGetAsync(string key);
        Task<bool> KeyDeleteAsync(string key);
        Task<bool> KeyExistsAsync(string key);
        Task<long> IncrementAsync(string key);
    }
}