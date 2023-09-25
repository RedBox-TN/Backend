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
	public bool TokenExists(string? key);

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
	/// <param name="expireAt">out, the expire datetime as unix milliseconds</param>
	/// <returns>String representing the authentication token</returns>
	public string Store(User user, out long expireAt);

	/// <summary>
	///     Store the current user, as pending until 2FA is confirmed
	/// </summary>
	/// <param name="user">The user</param>
	/// <param name="expireAt">out, the expire datetime as unix milliseconds</param>
	/// <returns>String representing the authentication token</returns>
	public string StorePending(User user, out long expireAt);

	/// <summary>
	///     Set the authentication of the user as complete, after 2FA is verified
	/// </summary>
	/// <param name="key">The token</param>
	/// <param name="expiresAt">out, the new expire datetime as unix milliseconds</param>
	public void SetCompleted(string key, out long expiresAt);

	/// <summary>
	///     DeleteAsync user from cache
	/// </summary>
	/// <param name="key">The token</param>
	public void DeleteAsync(string? key);

	/// <summary>
	///     Refresh the token, creating a new one
	/// </summary>
	/// <param name="oldToken">The current token</param>
	/// <param name="expiresAt">out, the new expire datetime as unix milliseconds</param>
	/// <returns>The new token</returns>
	public string RefreshToken(string oldToken, out long expiresAt);
}