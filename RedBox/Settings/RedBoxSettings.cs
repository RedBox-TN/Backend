namespace RedBox.Settings;

public class RedBoxSettings
{
	public string PasswordResetKey { get; set; } = null!;
	public int AesKeySize { get; set; } = 256;
	public int SaltSize { get; set; } = 16;
}