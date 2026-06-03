using Volo.Abp.Identity;

namespace HC.DocumentWorkflowInstances;

internal static class WorkflowInstanceLogHelper
{
    public static string FormatIdentityUserDisplayName(IdentityUser? user)
    {
        if (user == null)
        {
            return "---";
        }

        var fullName = $"{user.Surname} {user.Name}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.UserName ?? "---" : fullName;
    }
}
