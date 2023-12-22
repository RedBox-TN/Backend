namespace RedBoxAuth.Email_utility;

public interface IAuthEmailUtility
{
	public Task SendPasswordResetRequestAsync(string address, string username, string id);
	public Task SendAccountLockNotificationAsync(string address, string username);
	public Task SendLoginNotificationAsync(string username, string ip, string userAgent, DateTime timeStamp);
}