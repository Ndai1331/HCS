using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace HC.Chat.Conversations;

public class ConversationMember : Entity<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; protected set; }
    public virtual Guid ConversationId { get; protected set; }
    public virtual Guid UserId { get; protected set; }
    public virtual DateTime JoinedDate { get; protected set; }
    public virtual string Role { get; protected set; } // ADMIN / MEMBER For group management
    public virtual bool IsActive { get; protected set; }
    public virtual bool IsPinned { get; protected set; } // Per-user pin status
    public virtual DateTime? PinnedDate { get; protected set; } // When user pinned this conversation
    public virtual int UnreadMessageCount { get; protected set; } // Track unread messages for this user
    
    // Navigation
    public virtual Conversation Conversation { get; protected set; }
    
    protected ConversationMember()
    {
    }
    
    public ConversationMember(
        Guid id,
        Guid conversationId,
        Guid userId,
        string role = "MEMBER",
        Guid? tenantId = null)
        : base(id)
    {
        ConversationId = conversationId;
        UserId = userId;
        Role = role;
        JoinedDate = DateTime.UtcNow;
        IsActive = true;
        IsPinned = false;
        TenantId = tenantId;
    }
    
    // Methods
    public virtual void Pin()
    {
        IsPinned = true;
        PinnedDate = DateTime.UtcNow;
    }
    
    public virtual void Unpin()
    {
        IsPinned = false;
        PinnedDate = null;
    }
    
    public virtual void SetRole(string role)
    {
        Role = role;
    }
    
    public virtual void Deactivate()
    {
        IsActive = false;
    }
    
    public virtual void Activate()
    {
        IsActive = true;
    }
    
    public virtual void IncrementUnreadCount()
    {
        UnreadMessageCount++;
    }
    
    public virtual void DecrementUnreadCount(int count = 1)
    {
        UnreadMessageCount = Math.Max(0, UnreadMessageCount - count);
    }
    
    public virtual void ResetUnreadCount()
    {
        UnreadMessageCount = 0;
    }
    
    public virtual void SetUnreadCount(int count)
    {
        UnreadMessageCount = Math.Max(0, count);
    }
}
