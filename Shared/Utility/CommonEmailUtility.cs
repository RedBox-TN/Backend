using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Shared.Settings;

namespace Shared.Utility;

public class CommonEmailUtility : ICommonEmailUtility
{
	protected readonly CommonEmailSettings _emailSettings;

	public CommonEmailUtility(CommonEmailSettings emailSettings)
	{
		_emailSettings = emailSettings;
	}

	public async Task SendAsync(string toAddress, string subject, string body)
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

	public async Task SendAccountLockNotificationAsync(string address, string username)
	{
		throw new NotImplementedException();
	}
}