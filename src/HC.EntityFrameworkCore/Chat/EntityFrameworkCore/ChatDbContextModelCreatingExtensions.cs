using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.Users.EntityFrameworkCore;
using HC.Chat.Conversations;
using HC.Chat.Messages;
using HC.Chat.Users;
using HC.Chat;

namespace HC.Chat.EntityFrameworkCore;

public static class ChatDbContextModelCreatingExtensions
{
    public static void ConfigureChat(
        this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<ChatUser>(b =>
        {
                //Configure table & schema name
                b.ToTable(ChatDbProperties.DbTablePrefix + "Users", ChatDbProperties.DbSchema);

            b.ConfigureByConvention();
            b.ConfigureAbpUser();

            b.ApplyObjectExtensionMappings();
        });

        builder.Entity<Message>(b =>
        {
                //Configure table & schema name
                b.ToTable(ChatDbProperties.DbTablePrefix + "Messages", ChatDbProperties.DbSchema);

            b.ConfigureByConvention();

            b.Property(x => x.Text).IsRequired().HasColumnName(nameof(Message.Text)).HasMaxLength(ChatMessageConsts.MaxTextLength);
            b.Property(x => x.IsAllRead).HasColumnName(nameof(Message.IsAllRead));
            b.Property(x => x.ReadTime).HasColumnName(nameof(Message.ReadTime));
            
            // New properties
            b.Property(x => x.IsPinned).HasColumnName(nameof(Message.IsPinned));
            b.Property(x => x.PinnedDate).HasColumnName(nameof(Message.PinnedDate));
            b.Property(x => x.PinnedByUserId).HasColumnName(nameof(Message.PinnedByUserId));
            b.Property(x => x.ReplyToMessageId).HasColumnName(nameof(Message.ReplyToMessageId));
            b.Property(x => x.ConversationId).HasColumnName(nameof(Message.ConversationId));
            
            // Navigation
            b.HasOne(x => x.ReplyToMessage)
                .WithMany(x => x.Replies)
                .HasForeignKey(x => x.ReplyToMessageId)
                .OnDelete(DeleteBehavior.Restrict);

            // Anchor jump + keyset pagination: filter by conversation, order by CreationTime desc, tie-break by Id desc
            b.HasIndex(e => new { e.ConversationId, e.CreationTime, e.Id })
                .IsDescending(false, true, true)
                .HasDatabaseName("IX_ChatMessages_ConversationId_CreationTime_Id");

            b.ApplyObjectExtensionMappings();
        });
        
        builder.Entity<MessageFile>(b =>
        {
            b.ToTable(ChatDbProperties.DbTablePrefix + "MessageFiles", ChatDbProperties.DbSchema);
            
            b.ConfigureByConvention();
            
            b.Property(x => x.MessageId).IsRequired(false).HasColumnName(nameof(MessageFile.MessageId)); // Nullable for pre-upload pattern
            b.Property(x => x.FileName).IsRequired().HasMaxLength(ChatConsts.MaxFileNameLength).HasColumnName(nameof(MessageFile.FileName));
            b.Property(x => x.FilePath).IsRequired().HasMaxLength(1024).HasColumnName(nameof(MessageFile.FilePath));
            b.Property(x => x.ContentType).HasMaxLength(256).HasColumnName(nameof(MessageFile.ContentType));
            b.Property(x => x.FileSize).HasColumnName(nameof(MessageFile.FileSize));
            b.Property(x => x.FileExtension).HasMaxLength(16).HasColumnName(nameof(MessageFile.FileExtension));
            b.Property(x => x.CreationTime).IsRequired().HasColumnName(nameof(MessageFile.CreationTime));
            b.Property(x => x.CreatorId).HasColumnName(nameof(MessageFile.CreatorId));
            
            b.HasOne(x => x.Message)
                .WithMany()
                .HasForeignKey(x => x.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
            
            b.HasIndex(x => x.MessageId);
            
            b.ApplyObjectExtensionMappings();
        });

        builder.Entity<UserMessage>(b =>
        {
                //Configure table & schema name
                b.ToTable(ChatDbProperties.DbTablePrefix + "UserMessages", ChatDbProperties.DbSchema);

            b.ConfigureByConvention();

            b.Property(x => x.ChatMessageId).IsRequired().HasColumnName(nameof(UserMessage.ChatMessageId));
            b.Property(x => x.UserId).IsRequired().HasColumnName(nameof(UserMessage.UserId));
            b.Property(x => x.TargetUserId).HasColumnName(nameof(UserMessage.TargetUserId));
            b.Property(x => x.Side).HasColumnName(nameof(UserMessage.Side));
            b.Property(x => x.IsRead).HasColumnName(nameof(UserMessage.IsRead));
            b.Property(x => x.ReadTime).HasColumnName(nameof(UserMessage.ReadTime));

            b.HasOne<Message>().WithMany().HasForeignKey(p => p.ChatMessageId).OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(x => x.UserId);
            b.HasIndex(x => new { x.UserId, x.TargetUserId });

            b.ApplyObjectExtensionMappings();
        });

        builder.Entity<Conversation>(b =>
        {
                //Configure table & schema name
                b.ToTable(ChatDbProperties.DbTablePrefix + "Conversations", ChatDbProperties.DbSchema);

            b.ConfigureByConvention();
            
            // Core properties
            b.Property(x => x.Type).IsRequired().HasColumnName(nameof(Conversation.Type)).HasDefaultValue(ConversationType.User);
            b.Property(x => x.Name).IsRequired(false).HasMaxLength(ChatConsts.MaxConversationNameLength).HasColumnName(nameof(Conversation.Name));
            b.Property(x => x.Description).IsRequired(false).HasMaxLength(ChatConsts.MaxConversationDescriptionLength).HasColumnName(nameof(Conversation.Description));
            b.Property(x => x.ProjectId).HasColumnName(nameof(Conversation.ProjectId));
            b.Property(x => x.TaskId).HasColumnName(nameof(Conversation.TaskId));
            
            b.Property(x => x.LastMessage).IsRequired(false).HasColumnName(nameof(Conversation.LastMessage)).HasMaxLength(ChatMessageConsts.MaxTextLength);
            b.Property(x => x.LastMessageDate).HasColumnName(nameof(Conversation.LastMessageDate));

            b.HasIndex(x => x.Type);
            b.HasIndex(x => x.ProjectId);
            b.HasIndex(x => x.TaskId);

            b.ApplyObjectExtensionMappings();
        });
        
        builder.Entity<ConversationMember>(b =>
        {
            b.ToTable(ChatDbProperties.DbTablePrefix + "ConversationMembers", ChatDbProperties.DbSchema);
            
            b.ConfigureByConvention();
            
            b.Property(x => x.ConversationId).IsRequired().HasColumnName(nameof(ConversationMember.ConversationId));
            b.Property(x => x.UserId).IsRequired().HasColumnName(nameof(ConversationMember.UserId));
            b.Property(x => x.Role).HasMaxLength(50).HasColumnName(nameof(ConversationMember.Role)).HasDefaultValue("MEMBER");
            b.Property(x => x.IsActive).HasColumnName(nameof(ConversationMember.IsActive)).HasDefaultValue(true);
            b.Property(x => x.IsPinned).HasColumnName(nameof(ConversationMember.IsPinned)).HasDefaultValue(false);
            b.Property(x => x.PinnedDate).HasColumnName(nameof(ConversationMember.PinnedDate));
            b.Property(x => x.JoinedDate).IsRequired().HasColumnName(nameof(ConversationMember.JoinedDate));
            
            b.HasOne(x => x.Conversation)
                .WithMany(x => x.Members)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            
            b.HasIndex(x => x.ConversationId);
            b.HasIndex(x => x.UserId);
            b.HasIndex(x => new { x.UserId, x.IsPinned }); // Composite index for pinned queries
            b.HasIndex(x => new { x.ConversationId, x.UserId }).IsUnique(); // Unique constraint
            
            b.ApplyObjectExtensionMappings();
        });

        // Object extensions are configured in HCDbContextBase
    }
}
