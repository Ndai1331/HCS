using Microsoft.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using HC.Books;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.LanguageManagement.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.TextTemplateManagement.EntityFrameworkCore;
using Volo.Saas.EntityFrameworkCore;
//using Volo.Abp.BlobStoring.Database.EntityFrameworkCore;
using Volo.Abp.Gdpr;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.FileManagement.EntityFrameworkCore;
using HC.Chat.EntityFrameworkCore;
using HC.Chat.Conversations;
using HC.Chat.Messages;
using HC.Chat.Users;
// using Volo.Forms.EntityFrameworkCore;

namespace HC.EntityFrameworkCore;

public abstract class HCDbContextBase<TDbContext> : AbpDbContext<TDbContext>, IChatDbContext
    where TDbContext : DbContext
{
    public DbSet<Book> Books { get; set; }
    
    // Chat DbSets
    public DbSet<Message> ChatMessages { get; set; }
    public DbSet<ChatUser> ChatUsers { get; set; }
    public DbSet<UserMessage> ChatUserMessages { get; set; }
    public DbSet<Conversation> ChatConversations { get; set; }
    public DbSet<ConversationMember> ChatConversationMembers { get; set; }
    public DbSet<MessageFile> ChatMessageFiles { get; set; }
    
    public HCDbContextBase(DbContextOptions<TDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        /* Include modules to your migration db context */

        builder.ConfigurePermissionManagement();
        builder.ConfigureSettingManagement();
        builder.ConfigureBackgroundJobs();
        builder.ConfigureAuditLogging();
        builder.ConfigureIdentityPro();
        builder.ConfigureOpenIddictPro();
        builder.ConfigureFeatureManagement();
        builder.ConfigureLanguageManagement();
        builder.ConfigureFileManagement();
        builder.ConfigureSaas();
        builder.ConfigureChat();
        builder.ConfigureTextTemplateManagement();
        //builder.ConfigureBlobStoring();
        builder.ConfigureGdpr();
        
        builder.Entity<Book>(b =>
        {
            b.ToTable(HCConsts.DbTablePrefix + "Books",
                HCConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.Name).IsRequired().HasMaxLength(128);
        });

        /* Configure your own tables/entities inside here */

        //builder.Entity<YourEntity>(b =>
        //{
        //    b.ToTable(HCConsts.DbTablePrefix + "YourEntities", HCConsts.DbSchema);
        //    b.ConfigureByConvention(); //auto configure for the base class props
        //    //...
        //});

        //if (builder.IsHostDatabase())
        //{
        //    /* Tip: Configure mappings like that for the entities only available in the host side,
        //     * but should not be in the tenant databases. */
        //}
        // builder.ConfigureForms();
        }
}
