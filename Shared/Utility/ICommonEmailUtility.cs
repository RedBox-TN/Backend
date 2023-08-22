namespace Shared.Utility;

public interface ICommonEmailUtility
{
	public Task SendAsync(string toAddress, string subject, string body);
	public Task SendAccountLockNotificationAsync(string address, string username);
}