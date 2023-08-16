namespace RedBox.Settings;

public class RedBoxSettings
{
    public string PasswordResetKey { get; set; } = null!;
    public int AesKeySize { get; set; } = 256;
    public int PasswordTokenExpireMinutes { get; set; } = 5;
    public int EmailTokenExpireMinutes { get; set; } = 60;
    public int PasswordHistoryMax { get; set; } = 3;
    public int SaltSize { get; set; } = 16;
}