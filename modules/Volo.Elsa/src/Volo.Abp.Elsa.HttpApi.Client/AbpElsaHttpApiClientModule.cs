using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Volo.Abp.Elsa;

[DependsOn(
    typeof(AbpElsaApplicationContractsModule),
    typeof(AbpHttpClientModule))]
public class AbpElsaHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(
            typeof(AbpElsaApplicationContractsModule).Assembly,
            AbpElsaRemoteServiceConsts.RemoteServiceName
        );

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<AbpElsaHttpApiClientModule>();
        });

    }
}
