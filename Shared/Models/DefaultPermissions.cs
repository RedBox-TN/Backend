namespace Shared.Models;

public struct DefaultPermissions
{
	public const uint CreateChats = 1;
	public const uint CreateGroups = 2;
	public const uint ManageUsersAccounts = 4;
	public const uint EnableLocal2Fa = 8;
	public const uint ResetUsersPassword = 16;
	public const uint BlockUsers = 32;
	public const uint EnforceGlobal2Fa = 64;
	public const uint ForcePasswordChange = 128;
	public const uint ForceGlobalPasswordChange = 256;
	public const uint ReadOtherUsersChats = 512;
	public const uint DeleteSupervisedChat = 1024;
}