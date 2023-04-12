using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using RedBoxAuth.Authorization;
using RedBoxAuth.Cache;
using RedBoxAuth.Password_utility;
using RedBoxAuth.Security_hash_utility;
using RedBoxAuth.Services;
using RedBoxAuth.TOTP_utility;

namespace RedBoxAuth;

public static class RequiredAuthServices
{
	public static void AddRedBoxAuth(this IServiceCollection collection)
	{
		collection.AddSingleton<ITotpUtility, TotpUtility>();
		collection.AddSingleton<ISecurityHashUtility, SecurityHashUtility>();
		collection.AddSingleton<IPasswordUtility, PasswordUtility>();
		collection.AddSingleton<IAuthCache, AuthCache>();
		collection.AddHttpContextAccessor();
	}

	public static void UseRedBoxAuth(this WebApplication builder)
	{
		builder.UseMiddleware<AuthorizationMiddleware>();
		builder.MapGrpcService<AuthenticationService>();
	}
}