using Volo.Abp.Identity;
using Volo.Abp.Modularity;

namespace Volo.Abp.Elsa;

[DependsOn(
    typeof(AbpElsaAspNetCoreModule),
    typeof(AbpElsaDomainModule),
    typeof(AbpIdentityDomainModule)
)]
public class AbpElsaIdentityModule : AbpModule
{

}
