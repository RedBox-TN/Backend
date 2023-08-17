namespace RedBoxAuth.Settings;

/// <summary>
///     Configurable parameters for authentication and authorization mirroring the settings.json
/// </summary>
public class AuthSettings
{
    /// <summary>
    ///     Number of maximum consecutive failed login attempts
    /// </summary>
    public uint MaxLoginAttempts { get; set; } = 3;

    /// <summary>
    ///     TTL of user saved in redis waiting 2fa competition
    /// </summary>
    public uint PendingAuthMinutes { get; set; } = 15;

    /// <summary>
    ///     TTL of user saved in redis, it is also the TTL of the session if not refreshed
    /// </summary>
    public uint SessionExpireMinutes { get; set; } = 30;

    /// <summary>
    ///     Length of the token in bytes, to get the actual string length calculate N * 8 / 6, default 16 char
    /// </summary>
    public uint TokenSizeBytes { get; set; } = 12;

    /// <summary>
    ///     TTL of a user entry saved in local cache
    /// </summary>
    public uint LocalCacheExpirationScanMinutes { get; set; } = 5;

    /// <summary>
    ///     Size in byte of shared secret for 2fa
    /// </summary>
    public int TotpSharedSecretSize { get; set; } = 16;

    /// <summary>
    ///     Name of the totp issuer, it identify the totp codes in 2fa applications
    /// </summary>
    public string TotpIssuer { get; set; } = "RedBox";

    /// <summary>
    ///     Size in bytes of password salts
    /// </summary>
    public int HashSaltSize { get; set; } = 16;

    /// <summary>
    ///     Number of thread to use during hashing
    /// </summary>
    public int Argon2IdDegreeOfParallelism { get; set; } = 8;

    /// <summary>
    ///     Number of hashing iterations
    /// </summary>
    public int Argon2IdIterations { get; set; } = 40;

    /// <summary>
    ///     Memory cost of hashing
    /// </summary>
    public int Argon2IdMemorySizeKb { get; set; } = 8192;

    /// <summary>
    ///     Length of password hash
    /// </summary>
    public int Argon2IdHashSize { get; set; } = 256;

    /// <summary>
    ///     Pepper of password hash
    /// </summary>
    public string Argon2IdPepper { get; set; } = "";
}