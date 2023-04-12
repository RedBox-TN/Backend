namespace RedBoxAuth.Settings;

public class AuthDatabaseSettings : CommonDatabaseSettings
{
	public string UsersCollection { get; set; } = null!;
	public string RolesCollection { get; set; } = null!;
}