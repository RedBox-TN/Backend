namespace RedBoxAuth.Authorization;

/// <inheritdoc />
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequiredPermissionsAttribute : Attribute
{
	/// <summary>
	///     Requires the user to have the necessary permissions to use the method,
	///     to specify multiple permissions: Perm | Perm | ...Perm,
	///     Permissions can be found in the static struct Shared.Models.DefaultPermissions
	/// </summary>
	/// <param name="permissions">The required permissions</param>
	public RequiredPermissionsAttribute(uint permissions)
	{
		Permissions = permissions;
	}

	/// <summary>
	///     Get the required permissions
	/// </summary>
	public uint Permissions { get; }
}

/// <summary>
///     Requires that the user is logged in order to access method or class
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthenticationRequiredAttribute : Attribute
{
}
