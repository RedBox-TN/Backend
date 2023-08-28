namespace RedBox.Email_utility;

public interface IRedBoxEmailUtility
{
    public Task SendAccountLockNotificationAsync(string address, string username);
    public Task SendAccountCreationAsync(string address, string username, string name, string password);
    public Task SendEmailChangedAsync(string address, string id, string username);
    public Task SendAdminPasswordChangedAsync(string address, string password, string username);

    public Task SendPasswordChangedAsync(string address, string username);
    //TODO send notification email on password change from logged user
}