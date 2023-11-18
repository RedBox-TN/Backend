using System.Text;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Shared.Settings;

namespace RedBoxAuth.Healt_check;

public abstract class RedisHealthCheck : IHealthCheck
{
	private readonly CommonDatabaseSettings _dbSettings;

	protected RedisHealthCheck(IOptions<CommonDatabaseSettings> dbSettings)
	{
		_dbSettings = dbSettings.Value;
	}

	public abstract Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
		CancellationToken cancellationToken = default);

	protected async Task<(HealthStatus status, string message)> IsMongoHealthyAsync()
	{
		try
		{
			var mongoClient = new MongoClient(_dbSettings.ConnectionString);
			var rsStatus = await mongoClient.GetDatabase("admin")
				.RunCommandAsync<BsonDocument>("{ replSetGetStatus: 1 }");

			var sb = new StringBuilder();
			var bad = 0;
			foreach (var member in rsStatus["members"].AsBsonArray)
			{
				if (member.AsBsonDocument["health"].AsDouble != 0) continue;

				bad++;
				sb.AppendLine($"\t{member["name"].AsString}");
			}

			if (bad <= 0) return (HealthStatus.Healthy, "");

			sb.Insert(0,
				$"{bad} of {rsStatus["members"].AsBsonArray.Count} members are unavailable, in details:\n");

			return (HealthStatus.Degraded, sb.ToString());
		}
		catch (MongoException e)
		{
			return (HealthStatus.Unhealthy, e.Message);
		}
	}
}