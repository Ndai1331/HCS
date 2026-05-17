using System;
using HC.Chat.Conversations;
using HC.Chat.Messages;
using Microsoft.AspNetCore.Components;

namespace HC.Blazor.Pages.Chat1;

public partial class MessageItem
{
    private ulong _renderFingerprint;

    protected override bool ShouldRender()
    {
        var fp = ComputeRenderFingerprint();
        if (fp == _renderFingerprint)
        {
            return false;
        }

        _renderFingerprint = fp;
        return true;
    }

    private ulong ComputeRenderFingerprint()
    {
        if (Message == null)
        {
            return 0;
        }

        var hc = new HashCode();
        hc.Add(Message.Id);
        hc.Add(Message.Message ?? "");
        hc.Add(Message.MessageDate.Ticks);
        hc.Add(Message.IsRead);
        hc.Add(Message.IsPinned);
        hc.Add(Message.IsSending);
        hc.Add(Message.Side);
        hc.Add(Message.ReplyToMessageId);
        hc.Add(Message.ForwardedFromMessageId);
        hc.Add(Message.SenderUserId);
        hc.Add(Message.SenderUsername ?? "");
        hc.Add(Message.SenderName ?? "");
        hc.Add(Message.SenderSurname ?? "");
        hc.Add(Message.Files?.Count ?? 0);
        if (Message.Files != null)
        {
            foreach (var f in Message.Files)
            {
                hc.Add(f.Id);
            }
        }

        hc.Add(CurrentChatContact?.ConversationId);
        hc.Add(IsDeletingEnabled);

        unchecked
        {
            return (ulong)(uint)hc.ToHashCode();
        }
    }
}
