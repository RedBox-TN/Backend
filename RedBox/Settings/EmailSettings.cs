namespace RedBox.Settings;

public class EmailSettings
{
	public string FromAddress { get; set; } = null!;
	public string Password { get; set; } = null!;
	public string Host { get; set; } = null!;
	public int Port { get; set; } = 465;
	public bool EnableTls { get; set; } = false;
	public string ApplicationUrl { get; set; } = null!;
	public string PasswordResetUrl { get; set; } = null!;
	public string AccountCreationTemplateFile { get; set; } = null!;
	public string PasswordResetTemplateFile { get; set; } = null!;
	public string AccountLockedTemplateFile { get; set; } = null!;
}