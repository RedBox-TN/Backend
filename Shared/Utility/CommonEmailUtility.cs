using HandlebarsDotNet;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Shared.Settings;

namespace Shared.Utility;

public class CommonEmailUtility(IOptions<CommonEmailSettings> emailSettings)
{
	public readonly CommonEmailSettings EmailSettings = emailSettings.Value;

	public async Task SendAsync(string toAddress, string subject, string body)
	{
		var email = new MimeMessage();
		email.Sender = MailboxAddress.Parse(EmailSettings.FromAddress);
		email.To.Add(MailboxAddress.Parse(toAddress));
		email.Subject = subject;

		var builder = new BodyBuilder
		{
			HtmlBody = body
		};
		email.Body = builder.ToMessageBody();

		using var smtp = new SmtpClient();

		if (EmailSettings.EnableTls)
			await smtp.ConnectAsync(EmailSettings.Host, EmailSettings.Port, SecureSocketOptions.StartTls);
		else if (EmailSettings.EnableSsl)
			await smtp.ConnectAsync(EmailSettings.Host, EmailSettings.Port, SecureSocketOptions.SslOnConnect);
		else
			await smtp.ConnectAsync(EmailSettings.Host, EmailSettings.Port);

		await smtp.AuthenticateAsync(EmailSettings.FromAddress, EmailSettings.Password);
		await smtp.SendAsync(email);
		await smtp.DisconnectAsync(true);
	}

	public async Task SendAccountLockNotificationAsync(string toAddress, string username)
	{
		var template = Handlebars.Compile(await File.ReadAllTextAsync(EmailSettings.AccountLockedTemplateFile));
		var data = new
		{
			username
		};

		var body = template(data);
		if (body is null) throw new Exception("Unable to compile html template");

		await SendAsync(toAddress, "RedBox: il tuo account è stato bloccato", body);
	}
}