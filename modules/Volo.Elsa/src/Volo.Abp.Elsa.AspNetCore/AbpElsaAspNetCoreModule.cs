using Volo.Abp.Modularity;

namespace Volo.Abp.Elsa;

[DependsOn(
    typeof(AbpElsaApplicationContractsModule)
)]
public class AbpElsaAspNetCoreModule : AbpModule
{

}
