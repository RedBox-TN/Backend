using Shared.Models;

namespace RedBoxAuth.Cache;

/// <summary>
///     Expose methods for working whit authenticated users
/// </summary>
public interface IAuthCache : IBasicAuthCache
{
    /// <summary>
    ///     Check if the token correspond to a user authenticated
    /// </summary>
    /// <param name="key">The token</param>
    /// <returns>Bool, the result of the check</returns>
    public bool KeyExists(string? key);

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