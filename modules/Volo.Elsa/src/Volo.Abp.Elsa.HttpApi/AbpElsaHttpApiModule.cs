using Localization.Resources.AbpUi;
using Volo.Abp.Elsa.Localization;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Volo.Abp.Elsa;

[DependsOn(
    typeof(AbpElsaApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule))]
public class AbpElsaHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(AbpElsaHttpApiModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Get<AbpElsaResource>()
                .AddBaseTypes(typeof(AbpUiResource));
        });
    }
}
