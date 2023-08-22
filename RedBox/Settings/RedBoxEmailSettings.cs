namespace RedBox.Settings;

public abstract class RedBoxEmailSettings
{
	public string ApplicationUrl { get; set; } = null!;
	public string AccountCreationTemplateFile { get; set; } = null!;
	public string EmailConfirmUrl { get; set; } = null!;
	public string EmailConfirmTemplate { get; set; } = null!;
	public string NewPasswordTemplateFile { get; set; } = null!;
}