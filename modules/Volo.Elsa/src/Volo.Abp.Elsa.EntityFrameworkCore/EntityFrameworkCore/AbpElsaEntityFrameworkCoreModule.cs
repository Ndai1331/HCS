using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Volo.Abp.Elsa.EntityFrameworkCore;

[DependsOn(
    typeof(AbpElsaDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class AbpElsaEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<AbpElsaDbContext>(options =>
        {
            options.AddDefaultRepositories<IAbpElsaDbContext>(includeAllEntities: true);

            /* Add custom repositories here. Example:
            * options.AddRepository<Question, EfCoreQuestionRepository>();
            */
        });
    }
}
