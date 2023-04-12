namespace RedBoxAuth.Settings;

public class AuthenticationOptions
{
	public string UsersHashKey { get; set; } = "users";
	public uint MaxLoginAttempts { get; set; } = 3;
	public uint PendingAuthMinutes { get; set; } = 5;
	public uint SessionExpireMinutes { get; set; } = 30;
	public uint TokenSizeBytes { get; set; } = 12;
	public uint LocalCacheExpirationScanMinutes { get; set; } = 5;
	public int TotpSharedSecretSize { get; set; } = 16;
	public string TotpIssuer { get; set; } = "RedBox";
	public int HashSaltSize { get; set; } = 16;
	public int Argon2IdDegreeOfParallelism { get; set; } = 16;
	public int Argon2IdIterations { get; set; } = 4;
	public int Argon2IdMemorySizeKb { get; set; } = 512000;
	public int Argon2IdHashSize { get; set; } = 128;
	public string Argon2IdPepper { get; set; } = "";
}