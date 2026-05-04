using Volo.Abp.Modularity;

namespace Volo.Abp.Elsa;

[DependsOn(
    typeof(AbpElsaApplicationModule),
    typeof(ElsaDomainTestModule)
    )]
public class ElsaApplicationTestModule : AbpModule
{

}
