namespace RedBoxAuth.Settings;

public abstract class CommonDatabaseSettings
{
	public string ConnectionString { get; set; } = null!;
	public string DatabaseName { get; set; } = null!;
}