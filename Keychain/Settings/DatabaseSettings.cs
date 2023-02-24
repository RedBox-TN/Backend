namespace Keychain.Settings;

public class DatabaseSettings
{
	public string ConnectionString { get; set; } = null!;
	public string DatabaseName { get; set; } = null!;
	public string UsersPublicKeysCollection { get; set; } = null!;
	public string UsersPrivateKeysCollection { get; set; } = null!;
	public string GroupsPublicKeysCollection { get; set; } = null!;
	public string GroupsPrivateKeyCollection { get; set; } = null!;
	public string ExecutivesPublicKeysCollection { get; set; } = null!;
	public string ExecutivesPrivateKeysCollection { get; set; } = null!;
}