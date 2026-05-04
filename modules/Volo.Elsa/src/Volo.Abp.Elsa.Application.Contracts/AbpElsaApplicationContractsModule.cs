using Volo.Abp.Application;
using Volo.Abp.Modularity;
using Volo.Abp.Authorization;

namespace Volo.Abp.Elsa;

[DependsOn(
    typeof(AbpElsaDomainSharedModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
    )]
public class AbpElsaApplicationContractsModule : AbpModule
{

}
