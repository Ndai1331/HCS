using Microsoft.EntityFrameworkCore;
using Volo.Abp;

namespace Volo.Abp.Elsa.EntityFrameworkCore;

public static class AbpElsaDbContextModelCreatingExtensions
{
    public static void ConfigureElsa(
        this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        /* Configure all entities here. Example:

        builder.Entity<Question>(b =>
        {
            //Configure table & schema name
            b.ToTable(ElsaDbProperties.DbTablePrefix + "Questions", ElsaDbProperties.DbSchema);

            b.ConfigureByConvention();

            //Properties
            b.Property(q => q.Title).IsRequired().HasMaxLength(QuestionConsts.MaxTitleLength);

            //Relations
            b.HasMany(question => question.Tags).WithOne().HasForeignKey(qt => qt.QuestionId);

            //Indexes
            b.HasIndex(q => q.CreationTime);
        });
        */
    }
}
