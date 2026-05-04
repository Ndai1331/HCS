using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Volo.Abp.Elsa;

[DependsOn(
    typeof(AbpVirtualFileSystemModule)
    )]
public class AbpElsaInstallerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<AbpElsaInstallerModule>();
        });
    }
}
