using Microsoft.AspNetCore.Http;
using RedBoxAuth.Cache;
using RedBoxAuth.Security_hash_utility;

namespace RedBoxAuth.Authorization;

public class AuthorizationMiddleware
{
	private readonly IAuthCache _authCache;
	private readonly RequestDelegate _next;
	private readonly ISecurityHashUtility _securityHash;

	public AuthorizationMiddleware(IAuthCache authCache, ISecurityHashUtility securityHash, RequestDelegate next)
	{
		_authCache = authCache;
		_securityHash = securityHash;
		_next = next;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		var metadata = context.GetEndpoint()!.Metadata;

		if (metadata.Contains(new AnonymousAttribute()))
		{
			await _next(context);
			return;
		}

		if (!context.Request.Headers.TryGetValue(Constants.TokenHeaderName, out var key) ||
		    !_authCache.TryToGet(key, out var user) || !_securityHash.IsValid(user!.SecurityHash,
			    context.Request.Headers.UserAgent, context.Connection.RemoteIpAddress))
		{
			context.Response.StatusCode = StatusCodes.Status401Unauthorized;
			return;
		}

		if (metadata.Contains(new AuthenticationRequiredAttribute()))
		{
			await _next(context);
			return;
		}

		var requiredPermissions = metadata.GetMetadata<RequiredPermissionsAttribute>()!.Permission;

		if ((user.Role.Permissions & requiredPermissions) != requiredPermissions)
		{
			context.Response.StatusCode = StatusCodes.Status401Unauthorized;
			return;
		}

		await _next(context);
	}
}