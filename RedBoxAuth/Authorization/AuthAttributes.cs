namespace RedBoxAuth.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequiredPermissionsAttribute : Attribute
{
	public RequiredPermissionsAttribute(uint permission)
	{
		Permission = permission;
	}

	public uint Permission { get; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthenticationRequiredAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AnonymousAttribute : Attribute
{
}