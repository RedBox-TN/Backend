using Shared.Models;

namespace RedBoxAuth.Cache;

/// <summary>
///     Expose basic methods for interacting with the authenticated users
/// </summary>
public interface IBasicAuthCache
{
	/// <summary>
	///     Try to retrieve the user, if the token is invalid, return false
	/// </summary>
	/// <param name="key">The token</param>
	/// <param name="user">out, the user corresponding to the token</param>
	/// <returns>Bool, true if token is valid, false if not</returns>
	public bool TryToGet(string? key, out User? user);
}