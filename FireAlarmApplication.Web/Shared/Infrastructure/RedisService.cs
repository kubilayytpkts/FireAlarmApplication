using StackExchange.Redis;
using System.Text.Json;

namespace FireAlarmApplication.Web.Shared.Infrastructure;

/// <summary>
/// Redis cache implementation
/// StackExchange.Redis kullanarak cache operations
/// </summary>
public class RedisService : IRedisService
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisService> _logger;

    /// <summary>JSON serialization options</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public RedisService(IConnectionMultiplexer redis, ILogger<RedisService> logger)
    {
        _database = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var value = await _database.StringGetAsync(key);

            if (!value.HasValue)
            {
                _logger.LogDebug("Cache miss for key: {Key}", key);
                return default;
            }

            _logger.LogDebug("Cache hit for key: {Key}", key);
            return JsonSerializer.Deserialize<T>(value!, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache key: {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            await _database.StringSetAsync(key, json, expiry);

            _logger.LogDebug("Cache set for key: {Key} with expiry: {Expiry}", key, expiry?.ToString() ?? "None");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _database.KeyDeleteAsync(key);
            _logger.LogDebug("Cache key deleted: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting cache key: {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            return await _database.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking cache key existence: {Key}", key);
            return false;
        }
    }

    public async Task SetExpireAsync(string key, TimeSpan expiry)
    {
        try
        {
            await _database.KeyExpireAsync(key, expiry);
            _logger.LogDebug("Expiry set for key: {Key} to {Expiry}", key, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting expiry for key: {Key}", key);
        }
    }

    public async Task<Dictionary<string, T?>> GetMultipleAsync<T>(params string[] keys)
    {
        try
        {
            var redisKeys = keys.Select(k => (RedisKey)k).ToArray();
            var values = await _database.StringGetAsync(redisKeys);

            var result = new Dictionary<string, T?>();

            for (int i = 0; i < keys.Length; i++)
            {
                if (values[i].HasValue)
                {
                    result[keys[i]] = JsonSerializer.Deserialize<T>(values[i]!, JsonOptions);
                }
                else
                {
                    result[keys[i]] = default;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting multiple cache keys");
            return new Dictionary<string, T?>();
        }
    }
}