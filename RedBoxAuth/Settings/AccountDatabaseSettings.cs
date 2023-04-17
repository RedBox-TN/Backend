using Shared.Settings;

namespace RedBoxAuth.Settings;

/// <summary>
///     Parameters for authentication database
/// </summary>
public class AccountDatabaseSettings : CommonDatabaseSettings
{
	/// <summary>
	///     Name of the collection containing users
	/// </summary>
	public string UsersCollection { get; set; } = null!;

	/// <summary>
	///     Name of the collection containing roles
	/// </summary>
	public string RolesCollection { get; set; } = null!;
}