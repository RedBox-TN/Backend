using System.Net.Security;
using System.Text;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RedBoxAuth.Settings;
using StackExchange.Redis;

namespace RedBoxAuth.Healt_check;

public class RedisHealthCheck
{
	private readonly RedisSettings _settings;

	public RedisHealthCheck(IOptions<RedisSettings> settings)
	{
		_settings = settings.Value;
	}

	public async Task<(HealthStatus status, string message)> IsRedisHealthyAsync()
	{
		try
		{
			var redis = await ConnectionMultiplexer.ConnectAsync(_settings.ConnectionString,
				options => options.CertificateValidation += (_, _, _, errors) =>
				{
					return errors switch
					{
						SslPolicyErrors.None => true,
						SslPolicyErrors.RemoteCertificateNameMismatch => true,
						SslPolicyErrors.RemoteCertificateNotAvailable => false,
						SslPolicyErrors.RemoteCertificateChainErrors => false,
						_ => false
					};
				});

			var sb = new StringBuilder();
			var bad = 0;
			foreach (var server in redis.GetServers())
				try
				{
					await server.PingAsync(CommandFlags.NoRedirect);
				}
				catch (RedisException)
				{
					bad++;
					sb.AppendLine($"\t\u25cb {server.EndPoint}");
				}

			if (bad <= 0) return (HealthStatus.Healthy, "");

			sb.Insert(0,
				$"{bad} of {redis.GetServers().Length} servers are unavailable, in details:\n");

			return (HealthStatus.Degraded, sb.ToString());
		}
		catch (RedisException e)
		{
			return (HealthStatus.Unhealthy, e.Message);
		}
	}
}