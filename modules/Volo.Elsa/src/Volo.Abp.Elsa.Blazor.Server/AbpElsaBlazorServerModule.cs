using Volo.Abp.AspNetCore.Components.Server.Theming;
using Volo.Abp.Modularity;

namespace Volo.Abp.Elsa;

[DependsOn(
    typeof(AbpAspNetCoreComponentsServerThemingModule),
    typeof(AbpElsaBlazorModule)
    )]
public class AbpElsaBlazorServerModule : AbpModule
{

}
