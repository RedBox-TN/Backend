using Shared.Models;

namespace RedBoxAuth.Cache;

/// <summary>
///     Expose methods for working whit user stored in redis
/// </summary>
public interface IBasicAuthCache
{
	/// <summary>
	///     Try to retrieve the user, if the token is invalid, return false
	/// </summary>
	/// <param name="token">The token</param>
	/// <param name="user">out, the user corresponding to the token</param>
	/// <returns>Bool, true if token is valid, false if not</returns>
	public bool TryToGet(string? token, out User? user);

	/// <summary>
	///     DeleteAsync user from cache
	/// </summary>
	/// <param name="key">The token</param>
	public Task DeleteAsync(string? key);
}