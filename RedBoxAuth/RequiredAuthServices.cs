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

/// <summary>
///     Contains statics methods that add required components in order to allow authentication and authorization
/// </summary>
public static class RequiredAuthServices
{
	/// <summary>
	///     Add required dependencies for authentication and authorization
	/// </summary>
	/// <param name="builder">WebApplicationBuilder of the current application</param>
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

	/// <summary>
	///     Add required dependencies for basic user retrieval
	/// </summary>
	/// <param name="builder">WebApplicationBuilder of the current application</param>
	public static void AddUserRetrieval(this WebApplicationBuilder builder)
	{
		var redisHost = builder.Configuration.GetSection("Redis").GetSection("ConnectionString").Value;
		if (redisHost == null)
			Environment.Exit(-1);

		var redis = ConnectionMultiplexer.Connect(redisHost);

		builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
		builder.Services.AddSingleton<IBasicAuthCache, BasicAuthCache>();
	}

	/// <summary>
	///     Enable authentication and authorization services
	/// </summary>
	/// <param name="app">current WebApplication instance</param>
	public static void UseRedBoxAuthenticationAndAuthorization(this WebApplication app)
	{
		app.UseMiddleware<AuthorizationMiddleware>();
		app.MapGrpcService<AuthenticationService>();
	}
}