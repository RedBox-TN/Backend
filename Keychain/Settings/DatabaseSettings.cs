using Shared.Settings;

namespace Keychain.Settings;

public class DatabaseSettings : CommonDatabaseSettings
{
	public string UsersPublicKeysCollection { get; set; } = null!;
	public string UsersPrivateKeysCollection { get; set; } = null!;
	public string GroupsPublicKeysCollection { get; set; } = null!;
	public string GroupsPrivateKeyCollection { get; set; } = null!;
	public string SupervisorsPublicKeysCollection { get; set; } = null!;
	public string SupervisorsPrivateKeysCollection { get; set; } = null!;
}