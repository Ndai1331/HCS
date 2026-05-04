using System.Threading;
using System.Threading.Tasks;
using Elsa.Identity.Contracts;
using Elsa.Identity.Entities;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace Volo.Abp.Elsa;

public class AbpIdentityUserCredentialsValidator : IUserCredentialsValidator
{
    protected IdentityUserManager UserManager { get; }
    protected ITenantStore TenantStore { get; }

    public AbpIdentityUserCredentialsValidator(IdentityUserManager userManager, ITenantStore tenantStore)
    {
        UserManager = userManager;
        TenantStore = tenantStore;
    }

    public virtual async ValueTask<User?> ValidateAsync(string username, string password, CancellationToken cancellationToken = new CancellationToken())
    {
        var abpUser = await UserManager.FindByNameAsync(username);
        if (abpUser == null)
        {
            return null;
        }
        var isValidPassword = await UserManager.CheckPasswordAsync(abpUser, password);
        if (!isValidPassword)
        {
            return null;
        }

        var tenantName = abpUser.TenantId.HasValue
            ? (await TenantStore.FindAsync(abpUser.TenantId.Value))!.Name
            : null;

        var roles = await UserManager.GetRolesAsync(abpUser);

        return new User()
        {
            TenantId = tenantName,
            Id = abpUser.Id.ToString("D"),
            Name = abpUser.UserName,
            Roles = roles
        };
    }
}
