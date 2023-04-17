namespace Shared.Models;

public struct DefaultPermissions
{
	public const ushort CreateChats = 0b_1;
	public const ushort CreateGroups = 0b_10;
	public const ushort ModifyAccount = 0b_100;
	public const ushort EnableLocal2Fa = 0b_1000;
	public const ushort CreateUsers = 0b_1000_0;
	public const ushort DeleteUsers = 0b_1000_00;
	public const ushort ResetUsersPassword = 0b_1000_000;
	public const ushort BlockUsers = 0b_1000_0000;
	public const ushort EnforceGlobal2Fa = 0b_1000_0000_0;
	public const ushort ForcePasswordChange = 0b_1_0000_0000_0;
	public const ushort ForceGlobalPasswordChange = 0b_10_0000_0000_0;
}