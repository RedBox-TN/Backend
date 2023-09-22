using RedBox.Permission_Utility;
using Shared.Models;

namespace RedBox.PermissionUtility;

public class PermissionUtility : IPermissionUtility
{
    public bool IsCodeCorrect(uint code)
    {
        uint maxCode = 0;

        var permissions = new DefaultPermissions();
        var fieldInfo = permissions.GetType().GetFields();

        foreach (var permission in fieldInfo) maxCode += Convert.ToUInt32(permission.GetValue(permissions));

        return code <= maxCode;
    }
}