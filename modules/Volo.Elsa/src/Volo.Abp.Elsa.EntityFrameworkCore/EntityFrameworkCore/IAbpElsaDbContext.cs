using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Volo.Abp.Elsa.EntityFrameworkCore;

[ConnectionStringName(AbpElsaDbProperties.ConnectionStringName)]
public interface IAbpElsaDbContext : IEfCoreDbContext
{
    /* Add DbSet for each Aggregate Root here. Example:
     * DbSet<Question> Questions { get; }
     */
}
