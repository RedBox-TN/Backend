using System.Globalization;
using System.Web;
using HandlebarsDotNet;
using Microsoft.Extensions.Options;
using RedBoxAuth.Settings;
using Shared.Utility;

namespace RedBoxAuth.Email_utility;

public class AuthEmailUtility(
	IOptions<AuthEmailSettings> emailSettings,
	IEncryptionUtility encryptionUtility,
	CommonEmailUtility emailUtility)
	: IAuthEmailUtility
{
	private readonly AuthEmailSettings _emailSettings = emailSettings.Value;

	public async Task SendPasswordResetRequestAsync(string toAddress, string username, string id)
	{
		var expireAt = DateTimeOffset.Now.AddMinutes(_emailSettings.PasswordTokenExpireMinutes)
			.ToUnixTimeMilliseconds();

		var key = encryptionUtility.DeriveKey(_emailSettings.TokenEncryptionKey);

		var (encData, iv) = await encryptionUtility.AesEncryptAsync($"{id}#{expireAt}", key);

		var token = HttpUtility.UrlEncode(iv.Concat(encData).ToArray());

		var template = Handlebars.Compile(await File.ReadAllTextAsync(_emailSettings.PasswordResetTemplateFile));
		var data = new
		{
			username,
			url = $"{_emailSettings.PasswordResetUrl}{token}"
		};

		var body = template(data);
		if (body is null) throw new Exception("Unable to compile html template");

		await emailUtility.SendAsync(toAddress, "RedBox: reimposta la password", body);
	}

	public Task SendAccountLockNotificationAsync(string toAddress, string username)
	{
		return emailUtility.SendAccountLockNotificationAsync(toAddress, username);
	}


	public async Task SendLoginNotificationAsync(string username, string ip, string userAgent, DateTime timeStamp)
	{
		await emailUtility.SendAsync(_emailSettings.AddressLoginNotifications, $"Login di {username}",
			$"L'utente {username} si è loggato alle ore: {timeStamp.TimeOfDay.ToString()} con:<br>ip: {ip}<br>user agent: {userAgent}");
	}
}