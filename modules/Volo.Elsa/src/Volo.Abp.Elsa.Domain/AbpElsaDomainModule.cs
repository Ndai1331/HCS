using Volo.Abp.Domain;
using Volo.Abp.Modularity;
using Volo.Abp.Commercial.SuiteTemplates;

namespace Volo.Abp.Elsa;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(VoloAbpCommercialSuiteTemplatesModule),
    typeof(AbpElsaDomainSharedModule)
)]
public class AbpElsaDomainModule : AbpModule
{

}
