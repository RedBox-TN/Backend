using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace BackgroundServices;

public class Worker : BackgroundService
{
    private readonly Config _config;
    private readonly ILogger<Worker> _logger;
    private readonly IConnectionMultiplexer _redis;

    public Worker(IConnectionMultiplexer redis, IOptions<Config> config, ILogger<Worker> logger)
    {
        _redis = redis;
        _logger = logger;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _redis.GetDatabase().ExecuteAsync("CONFIG", "SET", "notify-keyspace-events", "Ex");
        RemoveExpiredFromHash(stoppingToken);
        RemoveDandlingFromHash(stoppingToken);
    }

    private void ExpiredKeysHandler(RedisChannel channel, RedisValue message)
    {
        var userHash = _redis.GetDatabase().HashGetAll(_config.UsersHashKey);
        if (userHash.Length == 0) return;

        var key = userHash.FirstOrDefault(e => e.Value.Equals(message)).Name;
        _redis.GetDatabase().HashDeleteAsync(_config.UsersHashKey, key);
        _logger.LogInformation("Removed expired key {Key} from hash at {Time}", key, DateTime.Now);
    }

    private async void RemoveExpiredFromHash(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _redis.GetSubscriber().SubscribeAsync(_config.ExpiredChannelName, ExpiredKeysHandler);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_config.ExpiredScanSleepSeconds), stoppingToken);
            }
            catch
            {
                return;
            }
        }
    }

    private async void RemoveDandlingFromHash(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var userHash = _redis.GetDatabase().HashGetAll(_config.UsersHashKey);
            if (!int.TryParse(_redis.GetDatabase().Execute("DBSIZE").ToString(), out var keys) ||
                userHash.Length <= keys - 1) continue;

            foreach (var hashEntry in userHash)
                if (!_redis.GetDatabase().KeyExists(hashEntry.Value.ToString()))
                {
                    _redis.GetDatabase().HashDelete(_config.UsersHashKey, hashEntry.Name);
                    _logger.LogInformation("Removed orphan key {Name} from hash at {Time}", hashEntry.Name,
                        DateTime.Now);
                }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_config.DandlingScanSleepMinutes), stoppingToken);
            }
            catch
            {
                return;
            }
        }
    }
}