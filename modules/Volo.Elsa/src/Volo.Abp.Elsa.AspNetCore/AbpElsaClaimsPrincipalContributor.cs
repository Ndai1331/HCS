using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Elsa.Permissions;
using Volo.Abp.Security.Claims;

namespace Volo.Abp.Elsa;

public class AbpElsaClaimsPrincipalContributor : IAbpClaimsPrincipalContributor, ITransientDependency
{
    protected ICurrentPrincipalAccessor PrincipalAccessor { get; }
    protected IPermissionChecker PermissionChecker { get; }

    public AbpElsaClaimsPrincipalContributor(ICurrentPrincipalAccessor principalAccessor, IPermissionChecker permissionChecker)
    {
        PrincipalAccessor = principalAccessor;
        PermissionChecker = permissionChecker;
    }

    public virtual async Task ContributeAsync(AbpClaimsPrincipalContributorContext context)
    {
        var identity = context.ClaimsPrincipal.Identities.FirstOrDefault();
        if (identity == null)
        {
            return;
        }

        using (PrincipalAccessor.Change(context.ClaimsPrincipal))
        {
            var elsaPermissions = AbpElsaPermissions.GetAll().ToList();
            elsaPermissions.RemoveAll(x => x == AbpElsaPermissions.GroupName);

            var permissions = new List<string>();
            foreach (var permission in elsaPermissions)
            {
                if (!await PermissionChecker.IsGrantedAsync(permission))
                {
                    continue;
                }

                permissions.Add(permission);
                if (permission != AbpElsaPermissions.AllPermission)
                {
                    continue;
                }

                permissions.RemoveAll(x => x != AbpElsaPermissions.AllPermission);
                break;
            }

            foreach (var permission in permissions)
            {
                identity.AddClaim(new Claim("permissions", permission));
            }
        }
    }
}
