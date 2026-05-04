using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Volo.Abp.Elsa.EntityFrameworkCore;

[ConnectionStringName(AbpElsaDbProperties.ConnectionStringName)]
public class AbpElsaDbContext : AbpDbContext<AbpElsaDbContext>, IAbpElsaDbContext
{
    /* Add DbSet for each Aggregate Root here. Example:
     * public DbSet<Question> Questions { get; set; }
     */

    public AbpElsaDbContext(DbContextOptions<AbpElsaDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ConfigureElsa();
    }
}
