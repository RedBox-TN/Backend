using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ExpiredKeyRemover;

public class Worker : BackgroundService
{
	private readonly Config _config;
	private readonly ILogger<Worker> _logger;
	private readonly IConnectionMultiplexer _redis;

	public Worker(ILogger<Worker> logger, IConnectionMultiplexer redis, IOptions<Config> config)
	{
		_logger = logger;
		_redis = redis;
		_config = config.Value;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await _redis.GetDatabase().ExecuteAsync("CONFIG", "SET", "notify-keyspace-events", "Ex");
		RemoveExpiredFromHash(stoppingToken);
		RemoveDandlingFromHash(stoppingToken);
	}

	private async void RemoveExpiredFromHash(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			await _redis.GetSubscriber().SubscribeAsync(_config.ExpiredChannelName, Handler);
			try
			{
				await Task.Delay(TimeSpan.FromSeconds(_config.ExpiredScanSleepSeconds), stoppingToken);
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
					await _redis.GetDatabase().HashDeleteAsync(_config.UsersHashKey, hashEntry.Value);

			try
			{
				await Task.Delay(TimeSpan.FromSeconds(_config.DandlingScanSleepMinutes), stoppingToken);
			}
			catch
			{
				return;
			}
		}
	}

	private void Handler(RedisChannel channel, RedisValue message)
	{
		var userHash = _redis.GetDatabase().HashGetAll(_config.UsersHashKey);
		if (userHash.Length == 0) return;

		var key = userHash.FirstOrDefault(e => e.Value.Equals(message)).Name;
		_redis.GetDatabase().HashDeleteAsync(_config.UsersHashKey, key);
		_redis.GetDatabase().KeyDelete(_config.UsersHashKey);
	}
}