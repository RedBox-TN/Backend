using Shared.Settings;

namespace Keychain.Settings;

public class DatabaseSettings : CommonDatabaseSettings
{
	public string ChatsKeysCollection { get; set; } = "ChatsKeys";
	public string GroupsKeysCollection { get; set; } = "GroupsKeys";
	public string SupervisorsMasterKeysCollection { get; set; } = "SupervisorsMasterKeys";
	public string SupervisedChatsKeysCollection { get; set; } = "SupervisedChatsKeys";
	public string SupervisedGroupsKeysCollection { get; set; } = "SupervisedGroupsKeys";
	public string SupervisorPrivateKeyCollection { get; set; } = "SupervisorPrivateKey";
	public string SupervisorPublicKeyCollection { get; set; } = "SupervisorPublicKey";
	public string UsersMasterKeysCollection { get; set; } = "UsersMasterKeys";
	public string UsersPrivateKeysCollection { get; set; } = "UsersPrivateKeys";
	public string UsersPublicKeysCollection { get; set; } = "UsersPublicKeys";
}