namespace RedBox.Settings;

public class DatabaseSettings
{
	public string ConnectionString { get; set; } = null!;
	public string DatabaseName { get; set; } = null!;
	public string ChatsCollection { get; set; } = null!;
	public string UsersCollection { get; set; } = null!;
	public string RolesCollection { get; set; } = null!;
	public string PowersCollection { get; set; } = null!;
}