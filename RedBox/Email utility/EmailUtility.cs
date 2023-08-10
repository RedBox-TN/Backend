using System.Security.Cryptography;
using System.Text;
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

	public async Task SendPasswordResetAsync(string toAddress, string username)
	{
		var key = _encryptionUtility.DeriveKey(_redBoxSettings.PasswordResetKey, _redBoxSettings.AesKeySize);
		var encrypted =
			await _encryptionUtility.EncryptAsync(Encoding.UTF8.GetBytes(username), key, _redBoxSettings.AesKeySize);
		var token = HttpUtility.UrlEncode(encrypted.EncData);
		var iv = HttpUtility.UrlEncode(encrypted.Iv);
		var salt = HttpUtility.UrlEncode(RandomNumberGenerator.GetBytes(_redBoxSettings.SaltSize));

		var template = Handlebars.Compile(await File.ReadAllTextAsync(_emailSettings.PasswordResetTemplateFile));
		var data = new
		{
			username,
			url = $"{token}{salt}{iv}"
		};

		var body = template(data);
		if (body is null) throw new Exception("Unable to compile html template");

		await SenAsync(toAddress, "RedBox: reimposta la password", body);
	}

	public async Task SendAccountLockNotification(string toAddress, string username)
	{
		var template = Handlebars.Compile(await File.ReadAllTextAsync(_emailSettings.AccountLockedTemplateFile));
		var data = new
		{
			username
		};

		var body = template(data);
		if (body is null) throw new Exception("Unable to compile html template");

		await SenAsync(toAddress, "RedBox: account bloccato", body);
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

		await SenAsync(toAddress, "Il tuo nuovo account RedBox", body);
	}

	private async Task SenAsync(string toAddress, string subject, string body)
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