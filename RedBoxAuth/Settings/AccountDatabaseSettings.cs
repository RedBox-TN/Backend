namespace RedBoxAuth.Settings;

public class AccountDatabaseSettings : CommonDatabaseSettings
{
	public string UsersCollection { get; set; } = null!;
	public string RolesCollection { get; set; } = null!;
}