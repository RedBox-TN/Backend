using RedBoxAuth.Settings;

namespace RedBox.Settings;

public class DatabaseSettings : CommonDatabaseSettings
{
	public string ChatsCollection { get; set; } = null!;
}