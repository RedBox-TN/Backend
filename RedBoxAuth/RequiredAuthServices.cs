using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using RedBoxAuth.Authorization;
using RedBoxAuth.Cache;
using RedBoxAuth.Password_utility;
using RedBoxAuth.Security_hash_utility;
using RedBoxAuth.Services;
using RedBoxAuth.Settings;
using RedBoxAuth.TOTP_utility;
using StackExchange.Redis;

namespace RedBoxAuth;

public static class RequiredAuthServices
{
	public static void AddRedBoxAuthenticationAndAuthorization(this WebApplicationBuilder builder)
	{
		builder.Services.Configure<AccountDatabaseSettings>(builder.Configuration.GetSection("UsersDB"));

		builder.Services.Configure<AuthenticationOptions>(builder.Configuration.GetSection("AuthenticationOptions"));

		var redisHost = builder.Configuration.GetSection("Redis").GetSection("ConnectionString").Value;
		if (redisHost == null)
			Environment.Exit(-1);

		var redis = ConnectionMultiplexer.Connect(redisHost);

		builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

		builder.Services.AddSingleton<ITotpUtility, TotpUtility>();
		builder.Services.AddSingleton<ISecurityHashUtility, SecurityHashUtility>();
		builder.Services.AddSingleton<IPasswordUtility, PasswordUtility>();
		builder.Services.AddSingleton<IAuthCache, AuthCache>();
		builder.Services.AddHttpContextAccessor();
	}

	public static void UseRedBoxAuthenticationAndAuthorization(this WebApplication builder)
	{
		builder.UseMiddleware<AuthorizationMiddleware>();
		builder.MapGrpcService<AuthenticationService>();
	}
}