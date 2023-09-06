namespace Shared.Models;

public struct DefaultPermissions
{
    public const uint CreateChats = 1;
    public const uint CreateGroups = 2;
    public const uint ManageUsersAccounts = 4;
    public const uint EnableLocal2Fa = 8;
    public const uint BlockUsers = 16;
    public const uint EnforceGlobal2Fa = 32;
    public const uint ReadOtherUsersChats = 64;
    public const uint DeleteSupervisedChat = 128;
    public const uint ManageRoles = 256;
}