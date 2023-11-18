using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using RedBoxAuth.Authorization;
using RedBoxAuth.Email_utility;
using RedBoxAuth.Password_utility;
using RedBoxAuth.Security_hash_utility;
using RedBoxAuth.Services;
using RedBoxAuth.Session_storage;
using RedBoxAuth.Settings;
using RedBoxAuth.TOTP_utility;
using Shared.Settings;
using Shared.Utility;
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
		builder.Services.Configure<CommonEmailSettings>(builder.Configuration.GetSection("EmailSettings"));
		builder.Services.Configure<AccountDatabaseSettings>(builder.Configuration.GetSection("UsersDB"));
		builder.Services.Configure<AuthEmailSettings>(builder.Configuration.GetSection("EmailSettings"));
		builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection("AuthSettings"));
		builder.Services.Configure<RedisSettings>(builder.Configuration.GetSection("Redis"));

		var redisHost = builder.Configuration.GetSection("Redis").GetSection("ConnectionString").Value;
		if (redisHost == null)
			Environment.Exit(-1);

		var redis = ConnectionMultiplexer.Connect(redisHost);

		builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
		builder.Services.AddSingleton<CommonEmailUtility>();

		builder.Services.AddSingleton<ISecurityHashUtility, SecurityHashUtility>();
		builder.Services.AddSingleton<IEncryptionUtility, EncryptionUtility>();
		builder.Services.AddSingleton<IAuthEmailUtility, AuthEmailUtility>();
		builder.Services.AddSingleton<IPasswordUtility, PasswordUtility>();
		builder.Services.AddSingleton<IBasicSessionStorage, SessionStorage>();
		builder.Services.AddSingleton<ITotpUtility, TotpUtility>();
		builder.Services.AddSingleton<ISessionStorage, SessionStorage>();
		builder.Services.AddHttpContextAccessor();
	}

	/// <summary>
	///     Add required dependencies for basic authorization of users and ability to access authenticated users
	/// </summary>
	/// <param name="builder">WebApplicationBuilder of the current application</param>
	public static void AddRedBoxBasicAuthorization(this WebApplicationBuilder builder)
	{
		builder.Services.Configure<RedisSettings>(builder.Configuration.GetSection("Redis"));

		var redisHost = builder.Configuration.GetSection("Redis").GetSection("ConnectionString").Value;
		if (redisHost == null)
			Environment.Exit(-1);

		var redis = ConnectionMultiplexer.Connect(redisHost);

		builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
		builder.Services.AddSingleton<IBasicSessionStorage, BasicSessionStorage>();
		builder.Services.AddSingleton<ISecurityHashUtility, SecurityHashUtility>();
	}

	/// <summary>
	///     Enable basic authorization middleware
	/// </summary>
	/// <param name="app">current WebApplication instance</param>
	public static void UseRedBoxBasicAuthorization(this WebApplication app)
	{
		app.UseMiddleware<AuthorizationMiddleware>();
	}

	/// <summary>
	///     Enable authentication and authorization services
	/// </summary>
	/// <param name="app">current WebApplication instance</param>
	public static void UseRedBoxAuthenticationAndAuthorization(this WebApplication app)
	{
		app.UseMiddleware<AuthorizationMiddleware>();
		app.MapGrpcService<AuthenticationService>().EnableGrpcWeb();
	}
}