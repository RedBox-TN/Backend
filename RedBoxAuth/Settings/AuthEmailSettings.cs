using Shared.Settings;

namespace RedBoxAuth.Settings;

public class AuthEmailSettings : CommonEmailSettings
{
	public string PasswordResetUrl { get; set; } = null!;
	public string PasswordResetTemplateFile { get; set; } = null!;
	public string AddressLoginNotifications { get; set; } = null!;
}