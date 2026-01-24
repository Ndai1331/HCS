using System;
using HC.Chat;
using HC.Chat.Conversations;
using HC.Chat.Messages;
using HC.Chat.Users;

namespace HC.Blazor.Pages.Chat1;

/// <summary>
/// Helper class for message-related operations.
/// Consolidates message logic and provides utility methods.
/// </summary>
public static class MessageHelper
{
    /// <summary>
    /// Checks if a received message is for the current conversation.
    /// Handles both user and group conversations.
    /// </summary>
    public static bool IsMessageForCurrentConversation(
        ChatMessageRdto message,
        ChatContactDto currentContact,
        Guid? currentConversationId,
        Guid currentUserId)
    {
        if (currentContact == null)
            return false;

        if (currentContact.Type == ConversationType.User)
        {
            // For user conversations, check ConversationId first if both have it
            if (message.ConversationId.HasValue && currentContact.ConversationId.HasValue)
            {
                return message.ConversationId.Value == currentContact.ConversationId.Value;
            }

            // Fallback: Check by sender for direct conversations
            return (currentContact.UserId == message.SenderUserId) || (message.SenderUserId == currentUserId);
        }
        else if (currentConversationId.HasValue)
        {
            // For group conversations: check ConversationId
            return message.ConversationId.HasValue && 
                   message.ConversationId.Value == currentConversationId.Value;
        }

        return false;
    }

    /// <summary>
    /// Finds a contact in the list by message metadata.
    /// </summary>
    public static ChatContactDto FindContactByMessage(
        System.Collections.Generic.List<ChatContactDto> contacts,
        ChatMessageRdto message,
        Guid currentUserId)
    {
        // Try to find by ConversationId first (works for all types)
        if (message.ConversationId.HasValue)
        {
            var contact = contacts.Find(c =>
                c.ConversationId.HasValue && c.ConversationId.Value == message.ConversationId.Value);
            
            if (contact != null)
                return contact;
        }

        // Fallback: For User type, find by UserId (sender)
        if (message.SenderUserId != currentUserId)
        {
            return contacts.Find(c =>
                c.Type == ConversationType.User && c.UserId == message.SenderUserId);
        }

        return null;
    }

    /// <summary>
    /// Gets display name for a sender.
    /// </summary>
    public static string GetSenderDisplayName(
        string senderName,
        string senderSurname,
        string senderUsername,
        string unknownUserText = "Unknown User")
    {
        if (!string.IsNullOrEmpty(senderName) || !string.IsNullOrEmpty(senderSurname))
        {
            return $"{senderName} {senderSurname}".Trim();
        }

        if (!string.IsNullOrEmpty(senderUsername))
        {
            return senderUsername;
        }

        return unknownUserText;
    }
}
