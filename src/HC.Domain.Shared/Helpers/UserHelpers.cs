using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Users;

namespace HC.Chat.Helpers;

public static class UserHelpers
{
    public static bool IsAdminRole(this ICurrentUser currentUser)
    {   
        return currentUser.Roles.Any(r => r.ToLower() == "admin");
    }

    public static bool IsSuperAdminRole(this ICurrentUser currentUser)
    {
        return currentUser.Roles.Any(r => r.ToLower() == "superadmin");
    }
}