using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using RedBoxAuth.Cache;
using RedBoxAuth.Models;
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

		User? user;
		StringValues key;

		if (metadata.Contains(new AuthenticationRequiredAttribute()))
		{
			if (!context.Request.Headers.TryGetValue(Constants.TokenHeaderName, out key) ||
			    !_authCache.TryToGet(key, out user) || !_securityHash.IsValid(user!.SecurityHash,
				    context.Request.Headers.UserAgent,
				    context.Connection.RemoteIpAddress))
			{
				context.Response.StatusCode = StatusCodes.Status401Unauthorized;
				return;
			}

			context.User.AddIdentity(new ClaimsIdentity(user));
			await _next(context);
			return;
		}

		var required = metadata.GetMetadata<RequiredPermissionsAttribute>()!.Permission;

		if (!context.Request.Headers.TryGetValue(Constants.TokenHeaderName, out key) ||
		    !_authCache.TryToGet(key, out user) || !_securityHash.IsValid(user!.SecurityHash,
			    context.Request.Headers.UserAgent,
			    context.Connection.RemoteIpAddress))
		{
			context.Response.StatusCode = StatusCodes.Status401Unauthorized;
			return;
		}

		if ((user.Role.Permissions & required) != required)
		{
			context.Response.StatusCode = StatusCodes.Status401Unauthorized;
			return;
		}

		context.User.AddIdentity(new ClaimsIdentity(user));
		await _next(context);
	}
}