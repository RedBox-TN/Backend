using System.Web;
using HandlebarsDotNet;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using RedBox.Encryption_utility;
using RedBox.Settings;

namespace RedBox.Email_utility;

public class EmailUtility : IEmailUtility
{
	private readonly EmailSettings _emailSettings;
	private readonly IEncryptionUtility _encryptionUtility;
	private readonly RedBoxSettings _redBoxSettings;

	public EmailUtility(IOptions<EmailSettings> emailSettings, IOptions<RedBoxSettings> redBoxSettings,
		IEncryptionUtility encryptionUtility)
	{
		_encryptionUtility = encryptionUtility;
		_emailSettings = emailSettings.Value;
		_redBoxSettings = redBoxSettings.Value;
	}

	public async Task SendPasswordResetRequestAsync(string toAddress, string id)
	{
		var expireAt = DateTimeOffset.Now.AddMinutes(_redBoxSettings.PasswordTokenExpireMinutes)
			.ToUnixTimeMilliseconds();

		var key = _encryptionUtility.DeriveKey(_redBoxSettings.PasswordResetKey, _redBoxSettings.AesKeySize);

		var (encData, iv) =
			await _encryptionUtility.AesEncryptAsync($"{id}#{expireAt}", key, _redBoxSettings.AesKeySize);

		var token = HttpUtility.UrlEncode(iv.Concat(encData).ToArray());

		var template = Handlebars.Compile(await File.ReadAllTextAsync(_emailSettings.PasswordResetTemplateFile));
		var data = new
		{
			id,
			url = $"{_emailSettings.PasswordResetUrl}{token}"
		};

		var body = template(data);
		if (body is null) throw new Exception("Unable to compile html template");

		await SendAsync(toAddress, "RedBox: reimposta la password", body);
	}

	public async Task SendAccountLockNotificationAsync(string toAddress, string username)
	{
		var template = Handlebars.Compile(await File.ReadAllTextAsync(_emailSettings.AccountLockedTemplateFile));
		var data = new
		{
			username
		};

		var body = template(data);
		if (body is null) throw new Exception("Unable to compile html template");

		await SendAsync(toAddress, "RedBox: il tuo account è stato bloccato", body);
	}

	public async Task SendAccountCreationAsync(string toAddress, string username, string name, string password)
	{
		var template = Handlebars.Compile(await File.ReadAllTextAsync(_emailSettings.AccountCreationTemplateFile));
		var data = new
		{
			name,
			username,
			password,
			url = _emailSettings.ApplicationUrl
		};

		var body = template(data);
		if (body is null) throw new Exception("Unable to compile html template");

		await SendAsync(toAddress, "Il tuo nuovo account RedBox", body);
	}

	public async Task SendPasswordChangedAsync(string toAddress, string password, string username)
	{
		var template = Handlebars.Compile(await File.ReadAllTextAsync(_emailSettings.NewPasswordTemplateFile));
		var data = new
		{
			password,
			username
		};

		var body = template(data);
		if (body is null) throw new Exception("Unable to compile html template");

		await SendAsync(toAddress, "RedBox: la tua nuova password temporanea", body);
	}

	public async Task SendEmailChangedAsync(string toAddress, string id, string username)
	{
		var expireAt = DateTimeOffset.Now.AddMinutes(_redBoxSettings.PasswordTokenExpireMinutes)
			.ToUnixTimeMilliseconds();

		var key = _encryptionUtility.DeriveKey(_redBoxSettings.PasswordResetKey, _redBoxSettings.AesKeySize);
		var (encData, iv) =
			await _encryptionUtility.AesEncryptAsync($"{toAddress}#{id}#{expireAt}", key, _redBoxSettings.AesKeySize);

		var token = HttpUtility.UrlEncode(iv.Concat(encData).ToArray());

		var template = Handlebars.Compile(await File.ReadAllTextAsync(_emailSettings.EmailConfirmTemplate));
		var data = new
		{
			id,
			username,
			url = $"{_emailSettings.EmailConfirmUrl}{token}"
		};

		var body = template(data);
		if (body is null) throw new Exception("Unable to compile html template");

		await SendAsync(toAddress, "RedBox: conferma il tuo nuovo indirizzo email", body);
	}

	private async Task SendAsync(string toAddress, string subject, string body)
	{
		var email = new MimeMessage();
		email.Sender = MailboxAddress.Parse(_emailSettings.FromAddress);
		email.To.Add(MailboxAddress.Parse(toAddress));
		email.Subject = subject;

		var builder = new BodyBuilder
		{
			HtmlBody = body
		};
		email.Body = builder.ToMessageBody();

		using var smtp = new SmtpClient();

		if (_emailSettings.EnableTls)
			await smtp.ConnectAsync(_emailSettings.Host, _emailSettings.Port, SecureSocketOptions.StartTls);
		else
			await smtp.ConnectAsync(_emailSettings.Host, _emailSettings.Port);
		await smtp.AuthenticateAsync(_emailSettings.FromAddress, _emailSettings.Password);
		await smtp.SendAsync(email);
		await smtp.DisconnectAsync(true);
	}
}