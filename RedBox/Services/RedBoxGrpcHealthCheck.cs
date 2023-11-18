using System.Text;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RedBox.Settings;
using Shared.Healt_check;

namespace RedBox.Services;

public class RedBoxGrpcHealthCheck : CommonGrpcHealthCheck
{
	public RedBoxGrpcHealthCheck(IOptions<RedBoxDatabaseSettings> dbSettings) : base(dbSettings)
	{
	}

	public override async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		var mongoCheck = await IsMongoHealthyAsync();
		var sb = new StringBuilder();

		switch (mongoCheck.status)
		{
			case DependingServiceStatus.Ok:
				sb.AppendLine("MongoDB is up and running");
				break;
			case DependingServiceStatus.Degraded:
				sb.AppendLine($"MongoDB is degraded:\n\t{mongoCheck.message}");
				break;
			default:
			case DependingServiceStatus.Ko:
				sb.AppendLine($"MongoDB is down:\n\t{mongoCheck.message}");
				break;
		}


		if (mongoCheck.status == DependingServiceStatus.Ko) return HealthCheckResult.Unhealthy(sb.ToString());
		if (mongoCheck.status == DependingServiceStatus.Degraded) return HealthCheckResult.Degraded(sb.ToString());

		return HealthCheckResult.Healthy(sb.ToString());
	}
}