using Volo.Abp.Modularity;

namespace Volo.Abp.Elsa;

[DependsOn(
    typeof(AbpElsaDomainModule),
    typeof(ElsaTestBaseModule)
)]
public class ElsaDomainTestModule : AbpModule
{

}
