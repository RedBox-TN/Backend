using Shared.Models;

namespace RedBoxAuth.Cache;

/// <summary>
///     Expose methods for working whit user stored in redis
/// </summary>
public interface IAuthCache : IBasicAuthCache
{
	/// <summary>
	///     Check if the token exists and correspond to a authenticated user
	/// </summary>
	/// <param name="key">The token</param>
	/// <returns>If toke exists true</returns>
	public Task<bool> TokenExistsAsync(string? key);

	/// <summary>
	///     Check if the user is already logged
	/// </summary>
	/// <param name="username">The username of the user</param>
	/// <param name="token">Out, the current user token</param>
	/// <param name="remainingTime">Out, the remaining time before key expiration</param>
	/// <returns>If the user is already logged true</returns>
	public bool IsUserAlreadyLogged(string? username, out string? token, out long remainingTime);

	/// <summary>
	///     Store the user in the cache
	/// </summary>
	/// <param name="user">The user</param>
	/// <returns>String representing the authentication token, and the expire date</returns>
	public Task<(string token, long expiresAt)> StoreAsync(User user);

	/// <summary>
	///     Store the current user, as pending until 2FA is confirmed
	/// </summary>
	/// <param name="user">The user</param>
	/// <returns>String representing the authentication token, and the expire date</returns>
	public Task<(string token, long expiresAt)> StorePendingAsync(User user);

	/// <summary>
	///     Set the authentication of the user as complete, after 2FA is verified
	/// </summary>
	/// <param name="key">The token</param>
	public Task<long> SetCompletedAsync(string key);

	/// <summary>
	///     Refresh the token, creating a new one
	/// </summary>
	/// <param name="oldToken">The current token</param>
	/// <returns>The new token and the expire time</returns>
	public Task<(string newToken, long expiresAt)> RefreshTokenAsync(string oldToken);
}