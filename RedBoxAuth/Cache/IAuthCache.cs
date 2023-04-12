using RedBoxAuth.Models;

namespace RedBoxAuth.Cache;

public interface IAuthCache
{
	public string Store(User user);
	public string StorePending(User user);
	public bool KeyExists(string? key);
	public void SetCompleted(string key);
	public void Delete(string? key);
	public bool TryToGet(string? key, out User? user);
	public void RefreshExpireTime(string key);
}