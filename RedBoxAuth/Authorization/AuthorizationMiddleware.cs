using Grpc.Core;
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
		if (context.GetEndpoint() is null)
		{
			await _next(context);
			return;
		}

		var metadata = context.GetEndpoint()!.Metadata;

		User? user;

		var permAttribute = metadata.GetMetadata<PermissionsRequiredAttribute>();

		if (permAttribute is not null)
			if (IsUserAuthenticated(context, out var validHash, out user) &&
			    HasPermission(user!, permAttribute.Permissions))
			{
				context.Items[Constants.UserContextKey] = user;
				await _next(context);
				return;
			}
			else if (validHash)
			{
				throw new RpcException(new Status(StatusCode.PermissionDenied, string.Empty));
			}
			else
			{
				throw new RpcException(new Status(StatusCode.Unauthenticated, "User must be reauthenticated"));
			}

		if (metadata.Contains(new AuthenticationRequiredAttribute()))
		{
			if (IsUserAuthenticated(context, out var validHash, out user))
			{
				context.Items[Constants.UserContextKey] = user;
				await _next(context);
				return;
			}

			if (validHash) throw new RpcException(new Status(StatusCode.PermissionDenied, string.Empty));

			await _authCache.DeleteAsync(context.Request.Headers[Constants.TokenHeaderName]);
			throw new RpcException(new Status(StatusCode.Unauthenticated, "User must be reauthenticated"));
		}

		await _next(context);
	}


	private bool IsUserAuthenticated(HttpContext context, out bool validHash, out User? user)
	{
		user = null;

		if (!context.Request.Headers.TryGetValue(Constants.TokenHeaderName, out var key) ||
		    !_authCache.TryToGet(key, out user))
		{
			validHash = true;
			return false;
		}

		validHash = _securityHash.IsValid(user!.SecurityHash, context.Request.Headers.UserAgent,
			context.Connection.RemoteIpAddress);

		return validHash;
	}

	/// <summary>
	///     Check if user has the required permission
	/// </summary>
	/// <param name="user">The user</param>
	/// <param name="requiredPerm">The required permissions (1 or more using | to separate)</param>
	public static bool HasPermission(User user, uint requiredPerm)
	{
		return (user.Role.Permissions & requiredPerm) == requiredPerm;
	}
}