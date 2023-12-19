using System.Text;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RedBox.Settings;
using RedBoxAuth.Health_check;
using RedBoxAuth.Settings;
using Shared.Health_check;

namespace RedBox.Services;

public class RedBoxGrpcHealthCheck : IHealthCheck
{
	private readonly MongoDbHealthCheck _mongoCheck;
	private readonly RedisHealthCheck _redisCheck;

	public RedBoxGrpcHealthCheck(IOptions<RedBoxDatabaseSettings> dbSettings, IOptions<RedisSettings> redisSettings)
	{
		_mongoCheck = new MongoDbHealthCheck(dbSettings);
		_redisCheck = new RedisHealthCheck(redisSettings);
	}

	public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext _, CancellationToken token = default)
	{
		var mongoCheckResult = await _mongoCheck.IsMongoHealthyAsync();

		var sb = new StringBuilder("\n", 300);
		var unHealty = false;
		var degraded = false;

		switch (mongoCheckResult.status)
		{
			case HealthStatus.Healthy:
				sb.AppendLine("\u25a0 MongoDB is up and running");
				break;
			case HealthStatus.Degraded:
				degraded = true;
				sb.AppendLine($"\u25a0 MongoDB is degraded:\n\t{mongoCheckResult.message}");
				break;
			default:
			case HealthStatus.Unhealthy:
				unHealty = true;
				sb.AppendLine($"\u25a0 MongoDB is down:\n\t{mongoCheckResult.message}");
				break;
		}

		var redisCheckResult = await _redisCheck.IsRedisHealthyAsync();

		switch (redisCheckResult.status)
		{
			case HealthStatus.Healthy:
				sb.AppendLine("\u25a0 Redis is up and running");
				break;
			case HealthStatus.Degraded:
				degraded = true;
				sb.AppendLine($"\u25a0 Redis is degraded:\n\t{redisCheckResult.message}");
				break;
			default:
			case HealthStatus.Unhealthy:
				unHealty = true;
				sb.AppendLine($"\u25a0 Redis is down:\n\t{redisCheckResult.message}");
				break;
		}

		var msg = sb.ToString().TrimEnd('\n');
		if (unHealty) return HealthCheckResult.Unhealthy(msg);

		return degraded ? HealthCheckResult.Degraded(msg) : HealthCheckResult.Healthy(msg);
	}
}