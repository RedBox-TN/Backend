namespace RedBoxAuth.Settings;

/// <summary>
///     Configurable parameters for Redis mirroring the settings.json
/// </summary>
public class RedisSettings
{
    /// <summary>
    ///     Index of database storing current authenticated users
    /// </summary>
    public int SessionDatabaseIndex { get; set; } = 0;

    /// <summary>
    ///     Index of database storing association between usernames of authenticated users and current token
    /// </summary>
    public int UsernameTokenDatabaseIndex { get; set; } = 1;
}