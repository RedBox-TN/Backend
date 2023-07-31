using Grpc.Core;
using Shared.Models;

namespace RedBoxAuth;

public static class HttpContextUserExtension
{
	/// <summary>
	/// Return current user from grpc context. Should only be used when you are sure that the user is authenticated
	/// </summary>
	/// <param name="context">Grpc context</param>
	/// <returns>The current user</returns>
	/// <exception cref="NullReferenceException">When user isn't authenticated</exception>
	public static User GetUser(this ServerCallContext context)
	{
		return context.GetHttpContext().Items[Constants.UserContextKey] as User ?? throw new NullReferenceException();
	}
}