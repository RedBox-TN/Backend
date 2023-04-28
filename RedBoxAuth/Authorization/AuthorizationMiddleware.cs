using Microsoft.AspNetCore.Http;
using RedBoxAuth.Cache;
using RedBoxAuth.Security_hash_utility;
using Shared.Models;

namespace RedBoxAuth.Authorization;

/// <summary>
///     Based on the applied attributes, provides access controls to incoming requests
/// </summary>
public class AuthorizationMiddleware
{
	private readonly IBasicAuthCache _authCache;
	private readonly RequestDelegate _next;
	private readonly ISecurityHashUtility _securityHash;

	/// <summary>
	///     Dependency injection constructor
	/// </summary>
	/// <param name="authCache"></param>
	/// <param name="securityHash"></param>
	/// <param name="next"></param>
	public AuthorizationMiddleware(IBasicAuthCache authCache, ISecurityHashUtility securityHash, RequestDelegate next)
	{
		_authCache = authCache;
		_securityHash = securityHash;
		_next = next;
	}

	/// <summary>
	///     Method invoked at every request
	/// </summary>
	/// <param name="context">Current Http context</param>
	public async Task InvokeAsync(HttpContext context)
	{
		var metadata = context.GetEndpoint()!.Metadata;

		User? user;

		var permAttribute = metadata.GetMetadata<RequiredPermissionsAttribute>();

		if (permAttribute is not null)
			if (IsUserAuthenticated(context, out user) &&
			    (user!.Role.Permissions & permAttribute.Permissions) == permAttribute.Permissions)
			{
				context.Items[Constants.UserContextKey] = user;
				await _next(context);
				return;
			}
			else
			{
				context.Response.StatusCode = StatusCodes.Status401Unauthorized;
				return;
			}

		if (metadata.Contains(new AuthenticationRequiredAttribute()))
		{
			if (IsUserAuthenticated(context, out user))
			{
				context.Items[Constants.UserContextKey] = user;
				await _next(context);
				return;
			}

			context.Response.StatusCode = StatusCodes.Status401Unauthorized;
			return;
		}

		await _next(context);
	}


	private bool IsUserAuthenticated(HttpContext context, out User? user)
	{
		user = null;
		return context.Request.Headers.TryGetValue(Constants.TokenHeaderName, out var key) &&
		        _authCache.TryToGet(key, out user) && _securityHash.IsValid(user!.SecurityHash,
			        context.Request.Headers.UserAgent, context.Connection.RemoteIpAddress);
	}
}