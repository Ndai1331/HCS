using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Services;
using Volo.Abp.Settings;
using Volo.Abp.Uow;
using Volo.Abp.Users;
using HC.Chat.Conversations;
using HC.Chat.Settings;
using HC.Chat.Users;
using HC.Chat;

namespace HC.Chat.Messages;

public class MessagingManager : DomainService
{
    public ICurrentUser CurrentUser { get; } //TODO: Don't use CurrentUser in the domain service

    private readonly IMessageRepository _messageRepository;
    private readonly IUserMessageRepository _userMessageRepository;
    private readonly IChatUserLookupService _chatUserLookupService;
    private readonly IConversationRepository _conversationRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ISettingProvider _settingProvider;

    public MessagingManager(
        IMessageRepository messageRepository,
        IUserMessageRepository userMessageRepository,
        IChatUserLookupService chatUserLookupService,
        IConversationRepository conversationRepository,
        ICurrentUser currentUser,
        IUnitOfWorkManager unitOfWorkManager,
        ISettingProvider settingProvider)
    {
        CurrentUser = currentUser;
        _messageRepository = messageRepository;
        _userMessageRepository = userMessageRepository;
        _chatUserLookupService = chatUserLookupService;
        _conversationRepository = conversationRepository;
        _unitOfWorkManager = unitOfWorkManager;
        _settingProvider = settingProvider;
    }

    public virtual async Task<Message> CreateNewMessage(Guid senderId, Guid receiverId, [NotNull] string messageText)
    {
        Check.NotNullOrWhiteSpace(messageText, nameof(messageText));

        var receiverUser = await _chatUserLookupService.FindByIdAsync(receiverId);
        if (receiverUser == null)
        {
            throw new BusinessException("HC.Chat:010002");
        }

        var message = await _messageRepository.InsertAsync(new Message(
            GuidGenerator.Create(),
            messageText,
            tenantId: CurrentTenant.Id
        ));

        await _userMessageRepository.InsertAsync(
            new UserMessage(GuidGenerator.Create(),
                senderId,
                message.Id,
                ChatMessageSide.Sender,
                receiverId,
                CurrentTenant.Id
            ));

        await _userMessageRepository.InsertAsync(
            new UserMessage(GuidGenerator.Create(),
                receiverId,
                message.Id,
                ChatMessageSide.Receiver,
                senderId,
                CurrentTenant.Id
            ));

        await CreateOrUpdateConversationWithNewMessageAsync(senderId, receiverId, messageText);

        return message;
    }

    public virtual async Task DeleteMessage( Guid messageId, Guid senderId, Guid targetUserId)
    {
        var userMessages = await _userMessageRepository.GetListAsync(messageId);
        if(userMessages.All(message => message.UserId != senderId))
        {
            return;
        }

        var message = await _messageRepository.GetAsync(messageId);
        if (message == null)
        {
            return;
        }

        await CheckDeletingMessageSetting(message);

        await _userMessageRepository.DeleteManyAsync(userMessages);
        await _messageRepository.DeleteAsync(message);
        if (_unitOfWorkManager.Current != null)
        {
            await _unitOfWorkManager.Current.SaveChangesAsync();
        }

        await UpdateConversationLastMessageAsync(senderId, targetUserId, message.Text);
    }

    public virtual async Task DeleteConversationAsync(Guid senderId, Guid targetId)
    {
        await CheckDeletingConversationSetting();
        var conversationPair = await _conversationRepository.FindPairAsync(senderId, targetId);
        if (conversationPair != null)
        {
            await _conversationRepository.DeleteManyAsync(new []{conversationPair.SenderConversation, conversationPair.TargetConversation});
        }

        var messageIds = await _userMessageRepository.GetAllMessageIdsAsync(senderId, targetId);
        if (messageIds.Any())
        {
            await _messageRepository.DeleteALlMessagesAsync(messageIds);
        }

        await _userMessageRepository.DeleteAllMessages(senderId, targetId);
    }

    private async Task CreateOrUpdateConversationWithNewMessageAsync(Guid senderId, Guid receiverId, string messageText)
    {
        var now = Clock.Now;
        var conversationPair = await _conversationRepository.FindPairAsync(senderId, receiverId);

        conversationPair ??= new ConversationPair();

        // TODO: This old logic is deprecated - User conversations should be created via CreateUserConversationAsync API
        // Keeping minimal compatibility for now
        if (conversationPair.SenderConversation == null)
        {
            var senderConversation = new Conversation(GuidGenerator.Create(), ConversationType.User, null, null, null, null, CurrentTenant.Id)
            {
                LastMessage = messageText,
                LastMessageDate = now
            };

            await _conversationRepository.InsertAsync(senderConversation);
            conversationPair.SenderConversation = senderConversation;

            if (_unitOfWorkManager.Current != null)
            {
                await _unitOfWorkManager.Current.SaveChangesAsync();
            }
        }

        if (conversationPair.TargetConversation == null)
        {
            var receiverConversation = new Conversation(GuidGenerator.Create(), ConversationType.User, null, null, null, null, CurrentTenant.Id)
            {
                LastMessage = messageText,
                LastMessageDate = now
            };

            await _conversationRepository.InsertAsync(receiverConversation);
            conversationPair.TargetConversation = receiverConversation;

            if (_unitOfWorkManager.Current != null)
            {
                await _unitOfWorkManager.Current.SaveChangesAsync();
            }
        }

        conversationPair.SenderConversation.SetLastMessage(messageText, now);
        conversationPair.TargetConversation.SetLastMessage(messageText, now);

        await _conversationRepository.UpdateAsync(conversationPair.SenderConversation);
        await _conversationRepository.UpdateAsync(conversationPair.TargetConversation);
    }

    private async Task UpdateConversationLastMessageAsync(Guid senderId, Guid receiverId, string deletedText)
    {
        var conversationPair = await _conversationRepository.FindPairAsync(senderId, receiverId);
        var lastMessage = await _userMessageRepository.GetLastMessageAsync(senderId, receiverId);
        var messageTime = Clock.Now;
        var messageText = string.Empty;

        if (lastMessage != null)
        {
            messageTime = lastMessage.Message.CreationTime;
            messageText = lastMessage.Message.Text;
        };

        if (conversationPair?.SenderConversation != null && conversationPair.SenderConversation.LastMessage == deletedText)
        {
            conversationPair.SenderConversation.SetLastMessage(messageText, messageTime, ignoreNullOrEmpty: true);
            await _conversationRepository.UpdateAsync(conversationPair.SenderConversation);
        }

        if (conversationPair?.TargetConversation != null && conversationPair.TargetConversation.LastMessage == deletedText)
        {
            conversationPair.TargetConversation.SetLastMessage(messageText, messageTime, ignoreNullOrEmpty: true);
            await _conversationRepository.UpdateAsync(conversationPair.TargetConversation);
        }
    }

    public async Task<List<MessageWithDetails>> ReadMessagesAsync(Guid targetUserId, int skipCount, int maxResultCount)
    {
        var conversationPair = await _conversationRepository.FindPairAsync(CurrentUser.GetId(), targetUserId);
        if (conversationPair != null)
        {
            // TODO: Update per-user read status in ConversationMember

            if (conversationPair.SenderConversation != null)
            {
                await _conversationRepository.UpdateAsync(conversationPair.SenderConversation);
            }

            if (conversationPair.TargetConversation != null)
            {
                await _conversationRepository.UpdateAsync(conversationPair.TargetConversation);
            }
        }

        var messages = new List<MessageWithDetails>();
        try
        {
            using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: _unitOfWorkManager.Current?.Options?.IsTransactional ?? false))
            {
                messages = await _userMessageRepository.GetMessagesAsync(CurrentUser.GetId(), targetUserId, skipCount, maxResultCount);

                //TODO: Optimize
                var readMessages = new List<Message>();
                foreach (var message in messages.Where(m => !m.UserMessage.IsRead).ToArray())
                {
                    message.UserMessage.MarkAsRead(Clock.Now);
                    await _userMessageRepository.UpdateAsync(message.UserMessage);

                    message.Message.MarkAsAllRead(Clock.Now);
                    readMessages.Add(message.Message);
                }

                foreach (var message in readMessages)
                {
                    await _messageRepository.UpdateAsync(message);
                }
                await uow.CompleteAsync();
            }
        }
        catch (AbpDbConcurrencyException e)
        {
            // The messages are change by another request. So, we can ignore this exception.
        }

        return messages;
    }

    public Task<bool> HasConversationAsync(Guid targetUserId)
    {
        return _userMessageRepository.HasConversationAsync(CurrentUser.GetId(), targetUserId);
    }
    
    public async Task<List<MessageWithDetails>> ReadMessagesByConversationIdAsync(Guid conversationId, int skipCount, int maxResultCount)
    {
        var currentUserId = CurrentUser.GetId();
        
        // Get conversation to verify membership
        var conversation = await _conversationRepository.GetWithMembersAsync(conversationId);
        if (conversation != null)
        {
            await EnsureConversationHistoryVisibleAsync(conversation, currentUserId);
            // TODO: Update per-user read status in ConversationMember
            await _conversationRepository.UpdateAsync(conversation);
        }
        
        var messages = await _userMessageRepository.GetMessagesByConversationIdAsync(conversationId, currentUserId, skipCount, maxResultCount);
        
        // Mark messages as read
        try
        {
            using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: _unitOfWorkManager.Current?.Options?.IsTransactional ?? false))
            {
                var readMessages = new List<Message>();
                foreach (var message in messages.Where(m => !m.UserMessage.IsRead).ToArray())
                {
                    message.UserMessage.MarkAsRead(Clock.Now);
                    await _userMessageRepository.UpdateAsync(message.UserMessage);

                    message.Message.MarkAsAllRead(Clock.Now);
                    readMessages.Add(message.Message);
                }

                foreach (var message in readMessages)
                {
                    await _messageRepository.UpdateAsync(message);
                }
                await uow.CompleteAsync();
            }
        }
        catch (AbpDbConcurrencyException e)
        {
            // The messages are change by another request. So, we can ignore this exception.
        }

        return messages;
    }

    private async Task EnsureConversationHistoryVisibleAsync(Conversation conversation, Guid currentUserId)
    {
        var isMember = conversation.Members.Any(m => m.UserId == currentUserId && m.IsActive);
        if (!isMember)
        {
            return;
        }

        var messages = await _messageRepository.GetByConversationIdAsync(conversation.Id);
        if (messages.Count == 0)
        {
            return;
        }

        var existingMessageIds = await _userMessageRepository.GetMessageIdsByConversationIdAsync(conversation.Id, currentUserId);
        if (existingMessageIds.Count == messages.Count)
        {
            return;
        }

        var targetUserId = conversation.Members
            .Where(m => m.IsActive && m.UserId != currentUserId)
            .Select(m => m.UserId)
            .FirstOrDefault();

        var missingUserMessages = new List<UserMessage>();
        foreach (var message in messages)
        {
            if (existingMessageIds.Contains(message.Id))
            {
                continue;
            }

            var side = message.CreatorId == currentUserId ? ChatMessageSide.Sender : ChatMessageSide.Receiver;
            var userMessage = new UserMessage(
                GuidGenerator.Create(),
                currentUserId,
                message.Id,
                side,
                targetUserId == Guid.Empty ? currentUserId : targetUserId,
                CurrentTenant.Id);

            // Historical messages should be visible immediately without being treated as unread.
            userMessage.MarkAsRead(Clock.Now);
            missingUserMessages.Add(userMessage);
        }

        if (missingUserMessages.Count > 0)
        {
            await _userMessageRepository.InsertManyAsync(missingUserMessages);
        }
    }

    protected virtual async Task CheckDeletingMessageSetting(Message message)
    {
        var deletingMessages = (ChatDeletingMessages)Enum.Parse(typeof(ChatDeletingMessages),(await _settingProvider.GetOrNullAsync(ChatSettingNames.Messaging.DeletingMessages))!);

        if (deletingMessages == ChatDeletingMessages.Disabled)
        {
            throw new BusinessException("HC.Chat:010007");
        }

        if (deletingMessages == ChatDeletingMessages.EnabledWithDeletionPeriod)
        {
            var deletionPeriod = await _settingProvider.GetAsync<int>(ChatSettingNames.Messaging.MessageDeletionPeriod);

            if(message.CreationTime.AddSeconds(deletionPeriod) < Clock.Now)
            {
                throw new BusinessException("HC.Chat:010005").WithData("seconds", deletionPeriod);
            }
        }
    }

    protected virtual async Task CheckDeletingConversationSetting()
    {
        var deletingMessages = (ChatDeletingMessages)Enum.Parse(typeof(ChatDeletingMessages),(await _settingProvider.GetOrNullAsync(ChatSettingNames.Messaging.DeletingMessages))!);
        if (deletingMessages != ChatDeletingMessages.Enabled)
        {
            throw new BusinessException("HC.Chat:010006");
        }

        var deletingConversations = (ChatDeletingConversations)Enum.Parse(typeof(ChatDeletingConversations),(await _settingProvider.GetOrNullAsync(ChatSettingNames.Messaging.DeletingConversations))!);
        if (deletingConversations == ChatDeletingConversations.Disabled)
        {
            throw new BusinessException("HC.Chat:010006");
        }
    }
}
