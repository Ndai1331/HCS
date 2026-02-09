using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Application;
using Volo.Abp.Emailing;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Volo.Forms;

[DependsOn(
    typeof(FormsDomainModule),
    typeof(FormsApplicationContractsModule),
    typeof(AbpMapperlyModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpEmailingModule)
    )]
public class FormsApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<FormsApplicationModule>();
        });

        context.Services.AddMapperlyObjectMapper<FormsApplicationModule>();
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        
    }
}
