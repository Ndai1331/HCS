using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HC.Chat.Messages;

public class Message : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; protected set; }

    [NotNull]
    public virtual string Text { get; protected set; }

    public virtual bool IsAllRead { get; protected set; }

    public virtual DateTime? ReadTime { get; protected set; }
    
    // New properties
    public virtual bool IsPinned { get; protected set; }
    public virtual DateTime? PinnedDate { get; protected set; }
    public virtual Guid? PinnedByUserId { get; protected set; }
    public virtual Guid? ReplyToMessageId { get; protected set; }
    public virtual Guid? ForwardedFromMessageId { get; protected set; } // For forwarded messages
    public virtual Guid? ConversationId { get; protected set; } // For group/project/task conversations
    
    // Navigation
    public virtual Message ReplyToMessage { get; protected set; }
    public virtual Message ForwardedFromMessage { get; protected set; } // Forwarded from original message
    public virtual ICollection<Message> Replies { get; protected set; }

    protected Message()
    {
        Replies = new List<Message>();
    }

    public Message(
        Guid id,
        [NotNull] string text,
        Guid? tenantId = null,
        Guid? conversationId = null)
        : base(id)
    {
        Text = Check.NotNullOrWhiteSpace(text, nameof(text), ChatMessageConsts.MaxTextLength);
        TenantId = tenantId;
        ConversationId = conversationId;
        Replies = new List<Message>();
    }
    
    public virtual void SetConversationId(Guid? conversationId)
    {
        ConversationId = conversationId;
    }

    public virtual void MarkAsAllRead(DateTime readTime)
    {
        IsAllRead = true;
        ReadTime = readTime;
    }
    
    public virtual void Pin(Guid pinnedByUserId)
    {
        IsPinned = true;
        PinnedDate = DateTime.UtcNow;
        PinnedByUserId = pinnedByUserId;
    }
    
    public virtual void Unpin()
    {
        IsPinned = false;
        PinnedDate = null;
        PinnedByUserId = null;
    }
    
    public virtual void SetReplyTo(Guid? replyToMessageId)
    {
        ReplyToMessageId = replyToMessageId;
    }
    
    public virtual void SetForwardedFrom(Guid? forwardedFromMessageId)
    {
        ForwardedFromMessageId = forwardedFromMessageId;
    }
}
