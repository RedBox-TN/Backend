using Shared.Settings;

namespace RedBox.Settings;

public class DatabaseSettings : CommonDatabaseSettings
{
	public string ChatsCollection { get; set; } = null!;
	public string GroupsCollection { get; set; } = null!;
	public string ChatsDatabase { get; set; } = null!;
	public string SupervisedChatsDatabase { get; set; } = null!;
	public string GroupsDatabase { get; set; } = null!;
	public string SupervisedGroupsDatabase { get; set; } = null!;
}