namespace Shared.Settings;

public class CommonEmailSettings
{
	public int PasswordTokenExpireMinutes { get; set; } = 5;
	public int EmailTokenExpireMinutes { get; set; } = 60;
	public string TokenEncryptionKey { get; set; } = null!;
	public string FromAddress { get; set; } = null!;
	public string Password { get; set; } = null!;
	public string Host { get; set; } = null!;
	public int Port { get; set; } = 465;
	public bool EnableTls { get; set; }
	public bool EnableSsl { get; set; }
	public string AccountLockedTemplateFile { get; set; } = null!;
}