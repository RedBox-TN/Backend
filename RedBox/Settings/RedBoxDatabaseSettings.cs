using Shared.Settings;

namespace RedBox.Settings;

public class RedBoxDatabaseSettings : CommonDatabaseSettings
{
	public string ChatDetailsCollection { get; set; } = "ChatDetails";
	public string GroupDetailsCollection { get; set; } = "GroupDetails";
	public string ChatsDatabase { get; set; } = "redbox-chats";
	public string GroupsDatabase { get; set; } = "redbox-groups";
	public string GridFsDatabase { get; set; } = "redbox-files";
	public int GridFsChunkSizeBytes { get; set; } = 5242880;
}