using RedBoxAuth.Models;

namespace RedBoxAuth.Cache;

public interface IAuthCache
{
	public string Store(User user, out uint expireAt);
	public string StorePending(User user, out uint expireAt);
	public bool KeyExists(string? key);
	public void SetCompleted(string key, out uint expiresAt);
	public void Delete(string? key);
	public bool TryToGet(string? key, out User? user);
	public string RefreshToken(string oldToken, out uint expiresAt);
}