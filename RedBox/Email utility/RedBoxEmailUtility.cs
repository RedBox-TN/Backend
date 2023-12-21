using System.Web;
using HandlebarsDotNet;
using Microsoft.Extensions.Options;
using RedBox.Settings;
using Shared.Utility;

namespace RedBox.Email_utility;

public class RedBoxEmailUtility(
	IOptions<RedBoxEmailSettings> emailSettings,
	IEncryptionUtility encryptionUtility,
	CommonEmailUtility commonEmailUtility)
	: IRedBoxEmailUtility
{
	private readonly RedBoxEmailSettings _redBoxEmailSettings = emailSettings.Value;

	public async Task SendAccountCreationAsync(string toAddress, string username, string name, string password)
	{
		var template =
			Handlebars.Compile(await File.ReadAllTextAsync(_redBoxEmailSettings.AccountCreationTemplateFile));
		var data = new
		{
			name,
			username,
			password,
			url = _redBoxEmailSettings.ApplicationUrl
		};

		var body = template(data);
		if (body is null) throw new Exception("Unable to compile html template");

		await commonEmailUtility.SendAsync(toAddress, "Il tuo nuovo account RedBox", body);
	}

	public async Task SendAdminPasswordChangedAsync(string toAddress, string password, string username)
	{
		var template = Handlebars.Compile(await File.ReadAllTextAsync(_redBoxEmailSettings.NewPasswordTemplateFile));
		var data = new
		{
			password,
			username
		};

		var body = template(data);
		if (body is null) throw new Exception("Unable to compile html template");

		await commonEmailUtility.SendAsync(toAddress, "RedBox: la tua nuova password temporanea", body);
	}

	public async Task SendEmailChangedAsync(string toAddress, string id, string username)
	{
		var expireAt = DateTimeOffset.Now.AddMinutes(commonEmailUtility.EmailSettings.EmailTokenExpireMinutes)
			.ToUnixTimeMilliseconds();

		var key = encryptionUtility.DeriveKey(commonEmailUtility.EmailSettings.TokenEncryptionKey);
		var (encData, iv) =
			await encryptionUtility.AesEncryptAsync($"{toAddress}#{id}#{expireAt}", key);

		var token = HttpUtility.UrlEncode(iv.Concat(encData).ToArray());

		var template = Handlebars.Compile(await File.ReadAllTextAsync(_redBoxEmailSettings.EmailConfirmTemplateFile));
		var data = new
		{
			id,
			username,
			url = $"{_redBoxEmailSettings.EmailConfirmUrl}{token}"
		};

		var body = template(data);
		if (body is null) throw new Exception("Unable to compile html template");

		await commonEmailUtility.SendAsync(toAddress, "RedBox: conferma il tuo nuovo indirizzo email", body);
	}

	public async Task SendPasswordChangedAsync(string toAddress, string username)
	{
		var template =
			Handlebars.Compile(await File.ReadAllTextAsync(_redBoxEmailSettings.ChangedPasswordTemplateFile));
		var data = new
		{
			username
		};

		var body = template(data);
		if (body is null) throw new Exception("Unable to compile html template");

		await commonEmailUtility.SendAsync(toAddress, "RedBox: password modificata", body);
	}

	public Task SendAccountLockNotificationAsync(string toAddress, string username)
	{
		return commonEmailUtility.SendAccountLockNotificationAsync(toAddress, username);
	}
}