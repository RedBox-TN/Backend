using System.Web;
using HandlebarsDotNet;
using Microsoft.Extensions.Options;
using RedBoxAuth.Settings;
using Shared.Utility;

namespace RedBoxAuth.Email_utility;

public class AuthEmailUtility : IAuthEmailUtility
{
	private readonly AuthEmailSettings _emailSettings;
	private readonly CommonEmailUtility _emailUtility;
	private readonly IEncryptionUtility _encryptionUtility;
	private readonly AuthEmailSettings _redBoxSettings;

	public AuthEmailUtility(IOptions<AuthEmailSettings> emailSettings, IOptions<AuthEmailSettings> redBoxSettings,
		IEncryptionUtility encryptionUtility, CommonEmailUtility emailUtility)
	{
		_encryptionUtility = encryptionUtility;
		_emailSettings = emailSettings.Value;
		_redBoxSettings = redBoxSettings.Value;
		_emailUtility = emailUtility;
	}

	public async Task SendPasswordResetRequestAsync(string toAddress, string username, string id)
	{
		var expireAt = DateTimeOffset.Now.AddMinutes(_redBoxSettings.PasswordTokenExpireMinutes)
			.ToUnixTimeMilliseconds();

		var key = _encryptionUtility.DeriveKey(_redBoxSettings.TokenEncryptionKey);

		var (encData, iv) = await _encryptionUtility.AesEncryptAsync($"{id}#{expireAt}", key);

		var token = HttpUtility.UrlEncode(iv.Concat(encData).ToArray());

		var template = Handlebars.Compile(await File.ReadAllTextAsync(_emailSettings.PasswordResetTemplateFile));
		var data = new
		{
			username,
			url = $"{_emailSettings.PasswordResetUrl}{token}"
		};

		var body = template(data);
		if (body is null) throw new Exception("Unable to compile html template");

		await _emailUtility.SendAsync(toAddress, "RedBox: reimposta la password", body);
	}

	public Task SendAccountLockNotificationAsync(string toAddress, string username)
	{
		return _emailUtility.SendAccountLockNotificationAsync(toAddress, username);
	}
}