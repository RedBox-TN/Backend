using Shared.Settings;

namespace RedBox.Settings;

public class DatabaseSettings : CommonDatabaseSettings
{
	public string ChatDetailsCollection { get; set; } = "ChatDetails";
	public string GroupDetailsCollection { get; set; } = "GroupDetails";
	public string ChatsDatabase { get; set; } = "redbox-chats";
	public string GroupsDatabase { get; set; } = "redbox-groups";
}