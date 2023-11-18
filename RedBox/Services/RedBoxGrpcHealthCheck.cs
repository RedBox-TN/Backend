using System.Text;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RedBox.Settings;
using Shared.Healt_check;

namespace RedBox.Services;

public class RedBoxGrpcHealthCheck : MongoDbHealthCheck
{
	public RedBoxGrpcHealthCheck(IOptions<RedBoxDatabaseSettings> dbSettings) : base(dbSettings)
	{
	}

	public override async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		var mongoCheck = await IsMongoHealthyAsync();
		var sb = new StringBuilder();

		var unHealty = false;
		var degraded = false;

		switch (mongoCheck.status)
		{
			case HealthStatus.Healthy:
				sb.AppendLine("MongoDB is up and running");
				break;
			case HealthStatus.Degraded:
				degraded = true;
				sb.AppendLine($"MongoDB is degraded:\n\t{mongoCheck.message}");
				break;
			default:
			case HealthStatus.Unhealthy:
				unHealty = true;
				sb.AppendLine($"MongoDB is down:\n\t{mongoCheck.message}");
				break;
		}

		if (unHealty) return HealthCheckResult.Unhealthy(sb.ToString());
		if (degraded) return HealthCheckResult.Degraded(sb.ToString());

		return HealthCheckResult.Healthy(sb.ToString());
	}
}