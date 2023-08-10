namespace RedBox.Email_utility;

public interface IEmailUtility
{
	public Task SendAccountCreationAsync(string address, string username, string name, string password);
	public Task SendPasswordResetAsync(string address, string username);
	public Task SendAccountLockNotification(string address, string username);
}