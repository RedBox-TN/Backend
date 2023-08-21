namespace RedBox.Email_utility;

public interface IEmailUtility
{
	public Task SendAccountCreationAsync(string address, string username, string name, string password);
	public Task SendPasswordResetRequestAsync(string address, string id);
	public Task SendAccountLockNotificationAsync(string address, string username);
	public Task SendEmailChangedAsync(string address, string id, string username);
	public Task SendPasswordChangedAsync(string address, string password, string username);
}