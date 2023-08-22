namespace RedBoxAuth.Email_utility;

public interface IAuthEmailUtility
{
	public Task SendPasswordResetRequestAsync(string address, string id);
	public Task SendAccountLockNotificationAsync(string address, string username);
}