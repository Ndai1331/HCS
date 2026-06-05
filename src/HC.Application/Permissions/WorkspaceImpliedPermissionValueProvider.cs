using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HC.Permissions;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Security.Claims;

namespace HC.Authorization.Permissions;

/// <summary>
/// Grants workspace-related module permissions when the user has HC.Workspace.
/// </summary>
public class WorkspaceImpliedPermissionValueProvider : PermissionValueProvider
{
    public const string ProviderName = "WorkspaceImplied";

    public WorkspaceImpliedPermissionValueProvider(IPermissionStore permissionStore)
        : base(permissionStore)
    {
    }

    public override string Name => ProviderName;

    public override async Task<PermissionGrantResult> CheckAsync(PermissionValueCheckContext context)
    {
        if (!WorkspaceImpliedPermissions.IsImplied(context.Permission.Name))
        {
            return PermissionGrantResult.Undefined;
        }

        if (await IsWorkspaceGrantedAsync(context.Principal))
        {
            return PermissionGrantResult.Granted;
        }

        return PermissionGrantResult.Undefined;
    }

    public override async Task<MultiplePermissionGrantResult> CheckAsync(PermissionValuesCheckContext context)
    {
        var result = new MultiplePermissionGrantResult();
        var isWorkspaceGranted = await IsWorkspaceGrantedAsync(context.Principal);

        foreach (var permission in context.Permissions)
        {
            if (!WorkspaceImpliedPermissions.IsImplied(permission.Name))
            {
                result.Result[permission.Name] = PermissionGrantResult.Undefined;
                continue;
            }

            result.Result[permission.Name] = isWorkspaceGranted
                ? PermissionGrantResult.Granted
                : PermissionGrantResult.Undefined;
        }

        return result;
    }

    private async Task<bool> IsWorkspaceGrantedAsync(ClaimsPrincipal? principal)
    {
        var userId = principal?.FindFirst(AbpClaimTypes.UserId)?.Value;
        if (!string.IsNullOrWhiteSpace(userId)
            && await PermissionStore.IsGrantedAsync(
                HCPermissions.Workspace.Default,
                UserPermissionValueProvider.ProviderName,
                userId))
        {
            return true;
        }

        var roleNames = principal?.FindAll(AbpClaimTypes.Role).Select(c => c.Value).ToList();
        if (roleNames == null || roleNames.Count == 0)
        {
            return false;
        }

        foreach (var roleName in roleNames)
        {
            if (await PermissionStore.IsGrantedAsync(
                    HCPermissions.Workspace.Default,
                    RolePermissionValueProvider.ProviderName,
                    roleName))
            {
                return true;
            }
        }

        return false;
    }
}
