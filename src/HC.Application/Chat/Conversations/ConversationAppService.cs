using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.BlobStoring;
using Volo.Abp.Data;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Features;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;
using Volo.Abp.Users;
using HC.Chat.Authorization;
using HC.Chat.Conversations;
using HC.Chat.Messages;
using HC.Chat.Users;
using Microsoft.Extensions.Logging;

namespace HC.Chat.Conversations;

[RequiresFeature(ChatFeatures.Enable)]
[Authorize(ChatPermissions.Messaging)]
public class ConversationAppService : ChatAppService, IConversationAppService
{
    private readonly MessagingManager _messagingManager;
    private readonly IChatUserLookupService _chatUserLookupService;
    private readonly IConversationRepository _conversationRepository;
    private readonly IConversationMemberRepository _conversationMemberRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IMessageFileRepository _messageFileRepository;
    private readonly IUserMessageRepository _userMessageRepository;
    private readonly IRealTimeChatMessageSender _realTimeChatMessageSender;
    private readonly IAuthorizationService _authorizationService;
    private readonly IPermissionFinder _permissionFinder;
    private readonly IBlobContainer _blobContainer;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly ILogger<ConversationAppService> _logger;
    private const string ForwardedMessageEmptyCommentPlaceholder = "📤";

    public ConversationAppService(
        MessagingManager messagingManager,
        IChatUserLookupService chatUserLookupService,
        IConversationRepository conversationRepository,
        IConversationMemberRepository conversationMemberRepository,
        IMessageRepository messageRepository,
        IMessageFileRepository messageFileRepository,
        IUserMessageRepository userMessageRepository,
        IRealTimeChatMessageSender realTimeChatMessageSender,
        IAuthorizationService authorizationService,
        IPermissionFinder permissionFinder,
        IBlobContainer blobContainer,
        IDistributedEventBus distributedEventBus,
        ILogger<ConversationAppService> logger)
    {
        _messagingManager = messagingManager;
        _chatUserLookupService = chatUserLookupService;
        _conversationRepository = conversationRepository;
        _conversationMemberRepository = conversationMemberRepository;
        _messageRepository = messageRepository;
        _messageFileRepository = messageFileRepository;
        _userMessageRepository = userMessageRepository;
        _realTimeChatMessageSender = realTimeChatMessageSender;
        _authorizationService = authorizationService;
        _permissionFinder = permissionFinder;
        _blobContainer = blobContainer;
        _distributedEventBus = distributedEventBus;
        _logger = logger;
    }

    private async Task ValidateChatUsersExistAsync(IEnumerable<Guid> userIds)
    {
        var distinct = userIds.Distinct().ToList();
        var users = await _chatUserLookupService.GetListByIdsAsync(distinct);
        if (users.Count != distinct.Count)
        {
            var found = users.Select(u => u.Id).ToHashSet();
            var missing = distinct.First(id => !found.Contains(id));
            throw new BusinessException("HC.Chat:UserNotFound").WithData("UserId", missing);
        }
    }

    /// <summary>
    /// Display label for a 1:1 (User) chat from the current member's perspective — always the other participant, not the stored <see cref="Conversation.Name"/> (which reflects the invitee at creation time).
    /// </summary>
    private static string FormatUserConversationDisplayName(string? name, string? surname, string? userName)
    {
        var full = $"{name} {surname}".Trim();
        return string.IsNullOrEmpty(full) ? (userName ?? string.Empty) : full;
    }

    private static string? FormatUserConversationDisplayName(ChatUser? user)
    {
        if (user == null)
        {
            return null;
        }

        return FormatUserConversationDisplayName(user.Name, user.Surname, user.UserName);
    }

    public virtual async Task<ChatMessageDto> SendMessageAsync(SendMessageInput input)
    {
        // ALL conversations now require ConversationId
        var conversation = await _conversationRepository.GetWithMembersAsync(input.ConversationId);
        if (conversation == null)
        {
            throw new BusinessException("HC.Chat:ConversationNotFound");
        }
        
        var currentUserId = CurrentUser.GetId();
        var isMember = await _conversationRepository.IsUserMemberAsync(input.ConversationId, currentUserId);
        if (!isMember)
        {
            throw new BusinessException("HC.Chat:UserNotMember");
        }
        
        Message message;
        List<Guid> memberUserIds;
        
        using (var uow = UnitOfWorkManager.Begin(requiresNew: true))
        {
            // Create message with ConversationId
            var messageText = input.Message ?? string.Empty;
            Check.NotNullOrWhiteSpace(messageText, nameof(input.Message));
            
            message = new Message(
                GuidGenerator.Create(),
                messageText,
                CurrentTenant.Id,
                input.ConversationId
            );
            await _messageRepository.InsertAsync(message);
            
            // Create UserMessage for all active members
            var activeMembers = conversation.Members.Where(m => m.IsActive).ToList();
            memberUserIds = activeMembers.Select(m => m.UserId).ToList();
            
            var targetIdForSide = activeMembers.FirstOrDefault(m => m.UserId != currentUserId)?.UserId ?? currentUserId;
            var userMessages = new List<UserMessage>(activeMembers.Count);
            foreach (var member in activeMembers)
            {
                var side = member.UserId == currentUserId ? ChatMessageSide.Sender : ChatMessageSide.Receiver;
                userMessages.Add(
                    new UserMessage(GuidGenerator.Create(), member.UserId, message.Id, side, targetIdForSide, CurrentTenant.Id));
            }

            await _userMessageRepository.InsertManyAsync(userMessages);
            
            // Update LastMessage for the conversation (shared by all members)
            var now = Clock.Now;
            conversation.SetLastMessage(messageText, now);
            await _conversationRepository.UpdateAsync(conversation);
            
            var membersToBumpUnread = activeMembers.Where(m => m.UserId != currentUserId).ToList();
            foreach (var member in membersToBumpUnread)
            {
                member.IncrementUnreadCount();
            }

            if (membersToBumpUnread.Count > 0)
            {
                await _conversationMemberRepository.UpdateManyAsync(membersToBumpUnread);
            }
            
            await uow.CompleteAsync();
        }

        var senderUser = await _chatUserLookupService.FindByIdAsync(CurrentUser.GetId());
        var messageDto = new ChatMessageRdto
        {
            Id = message.Id,
            ConversationId = input.ConversationId,
            ConversationType = conversation.Type.ToString(),
            ConversationName = conversation.Type == ConversationType.User
                ? (FormatUserConversationDisplayName(senderUser) ?? conversation.Name)
                : conversation.Name,
            SenderName = senderUser.Name,
            SenderSurname = senderUser.Surname,
            SenderUserId = senderUser.Id,
            SenderUsername = senderUser.UserName,
            Text = input.Message
        };
        
        // For User (1-1) conversation: send to other user only
        // For Group/Project/Task: send to all members except sender
        var recipientUserIds = conversation.Type == ConversationType.User 
            ? memberUserIds.Where(id => id != currentUserId).ToList()
            : memberUserIds.Where(id => id != currentUserId).ToList(); // For now, same logic, but can be customized
        
        foreach (var recipientUserId in recipientUserIds)
        {
            await _realTimeChatMessageSender.SendAsync(recipientUserId, messageDto);
        }
        
        return await MapToChatMessageDtoAsync(message, ChatMessageSide.Sender, message.CreatorId);
    }
 
    public virtual async Task DeleteMessageAsync(DeleteMessageInput input)
    {
        var message = await _messageRepository.GetAsync(input.MessageId);
        if (message?.ConversationId.HasValue == true)
        {
            var conversation = await _conversationRepository.GetWithMembersAsync(message.ConversationId.Value);
            if (conversation == null)
            {
                throw new BusinessException("HC.Chat:ConversationNotFound");
            }

            var currentUserId = CurrentUser.GetId();
            var currentMember = conversation.Members.FirstOrDefault(m => m.UserId == currentUserId && m.IsActive);
            if (currentMember == null)
            {
                throw new BusinessException("HC.Chat:UserNotMember");
            }

            var isOwnMessage = message.CreatorId.HasValue && message.CreatorId.Value == currentUserId;
            var isAdmin = string.Equals(currentMember.Role, "ADMIN", StringComparison.OrdinalIgnoreCase);
            if (conversation.Type != ConversationType.User && !isOwnMessage && !isAdmin)
            {
                throw new BusinessException("HC.Chat:OnlyAdminCanDeleteOthersMessages");
            }
        }

        await _messagingManager.DeleteMessage(input.MessageId, CurrentUser.GetId(), input.TargetUserId);
        
        await _realTimeChatMessageSender.DeleteMessageAsync(
            input.TargetUserId,
            input.MessageId
        );
    }

    public virtual async Task<ChatConversationDto> GetConversationAsync(GetConversationInput input)
    {
        // Support both Direct (via TargetUserId) and Group/Project/Task (via ConversationId)
        Conversation conversation = null;
        ChatTargetUserInfo targetUserInfo = null;
        
        if (input.ConversationId.HasValue)
        {
            // Group/Project/Task conversation
            conversation = await _conversationRepository.GetWithMembersAsync(input.ConversationId.Value);
            if (conversation == null)
            {
                throw new BusinessException("HC.Chat:ConversationNotFound");
            }
            
            var currentUserId = CurrentUser.GetId();
            var isMember = await _conversationRepository.IsUserMemberAsync(input.ConversationId.Value, currentUserId);
            if (!isMember)
            {
                throw new BusinessException("HC.Chat:UserNotMember");
            }
        }
        else
        {
            // Direct conversation (backward compatible)
            var targetUser = await _chatUserLookupService.FindByIdAsync(input.TargetUserId);
            if (targetUser == null)
            {
                throw new BusinessException("HC.Chat:010003");
            }
            
            targetUserInfo = new ChatTargetUserInfo
            {
                UserId = targetUser.Id,
                Name = targetUser.Name,
                Surname = targetUser.Surname,
                Username = targetUser.UserName,
            };
        }

        var chatConversation = new ChatConversationDto
        {
            TargetUserInfo = targetUserInfo,
            Messages = new List<ChatMessageDto>()
        };

        // Get messages - ALL conversations now use ConversationId
        List<MessageWithDetails> messages;
        Guid conversationId;
        
        if (input.ConversationId.HasValue)
        {
            conversationId = input.ConversationId.Value;
        }
        else
        {
            // Backward compatibility: Convert TargetUserId to ConversationId
            var conversationPair = await _conversationRepository.FindPairAsync(CurrentUser.GetId(), input.TargetUserId);
            if (conversationPair?.SenderConversation == null)
            {
                // No conversation exists - return empty
                return chatConversation;
            }
            conversationId = conversationPair.SenderConversation.Id;
        }
        
        messages = await _messagingManager.ReadMessagesByConversationIdAsync(conversationId, input.SkipCount, input.MaxResultCount);

        var messageEntities = messages.ConvertAll(x => x.Message);
        var sides = messages.ConvertAll(x => x.UserMessage.Side);
        chatConversation.Messages = await MapToChatMessageDtosBatchAsync(messageEntities, sides, senderUserIdOverrides: null);

        return chatConversation;
    }

    public virtual async Task MarkConversationAsReadAsync(MarkConversationAsReadInput input)
    {
        // TODO: Refactor to use ConversationMember-based unread tracking
        // Old logic used Conversation.UnreadMessageCount which is removed
        // New logic should update ConversationMember.LastReadMessageId or similar
        await Task.CompletedTask;
    }
    
    public async Task DeleteConversationAsync(DeleteConversationInput input)
    {
        if (input.ConversationId.HasValue)
        {
            var currentUserId = CurrentUser.GetId();
            var conversation = await _conversationRepository.GetWithMembersAsync(input.ConversationId.Value);
            if (conversation == null)
            {
                throw new BusinessException("HC.Chat:ConversationNotFound");
            }

            var activeMembers = conversation.Members.Where(m => m.IsActive).ToList();
            var currentMember = activeMembers.FirstOrDefault(m => m.UserId == currentUserId);
            if (currentMember == null || currentMember.Role != "ADMIN")
            {
                throw new BusinessException("HC.Chat:OnlyAdminCanDeleteConversation");
            }

            var membersToNotify = activeMembers
                .Where(m => m.UserId != currentUserId)
                .Select(m => m.UserId)
                .Distinct()
                .ToList();

            await _conversationRepository.DeleteAsync(conversation);

            foreach (var memberUserId in membersToNotify)
            {
                await _realTimeChatMessageSender.DeleteConversationAsync(
                    memberUserId,
                    currentUserId,
                    conversation.Id
                );
            }
        }
        else
        {
            await _messagingManager.DeleteConversationAsync(CurrentUser.GetId(), input.TargetUserId);

            await _realTimeChatMessageSender.DeleteConversationAsync(
                input.TargetUserId,
                CurrentUser.GetId(),
                null
            );
        }
    }
    
    // New methods for expanded features
    public virtual async Task<ConversationDto> CreateUserConversationAsync(CreateUserConversationInput input)
    {
        var currentUserId = CurrentUser.GetId();
        
        // Validate target user exists
        var targetUser = await _chatUserLookupService.FindByIdAsync(input.TargetUserId);
        if (targetUser == null)
        {
            throw new BusinessException("HC.Chat:UserNotFound").WithData("UserId", input.TargetUserId);
        }
        
        if (currentUserId == input.TargetUserId)
        {
            throw new BusinessException("HC.Chat:CannotChatWithYourself");
        }
        
        // Check if a User conversation already exists between these 2 users
        var existingMembers = await _conversationMemberRepository.GetByUserIdsAsync(
            new List<Guid> { currentUserId, input.TargetUserId },
            ConversationType.User);
        if (existingMembers.Count > 0)
        {
            return await MapToConversationDtoAsync(existingMembers.First().Conversation, input.TargetUserId);
        }
        
        Conversation conversation;
        using (var uow = UnitOfWorkManager.Begin(requiresNew: true))
        {
            // Create SINGLE conversation for 2 users
            conversation = new Conversation(
                GuidGenerator.Create(),
                ConversationType.User,
                input.Name ?? $"{targetUser.Name} {targetUser.Surname}".Trim(), // Default name
                null, // No description
                null, // No project
                null, // No task
                CurrentTenant.Id
            );
            // Initialize LastMessage properties
            conversation.LastMessage = string.Empty;
            conversation.LastMessageDate = Clock.Now;
            await _conversationRepository.InsertAsync(conversation);
            
            // Add 2 members (both as MEMBER role)
            var currentUserMember = new ConversationMember(
                GuidGenerator.Create(),
                conversation.Id,
                currentUserId,
                "MEMBER",
                CurrentTenant.Id
            );
            await _conversationMemberRepository.InsertAsync(currentUserMember);
            
            var targetUserMember = new ConversationMember(
                GuidGenerator.Create(),
                conversation.Id,
                input.TargetUserId,
                "MEMBER",
                CurrentTenant.Id
            );
            await _conversationMemberRepository.InsertAsync(targetUserMember);
            
            await uow.CompleteAsync();
        }
        
        // Publish event to notify target user about new conversation
        var currentUser = await _chatUserLookupService.FindByIdAsync(currentUserId);
        await _distributedEventBus.PublishAsync(new ConversationCreatedEto
        {
            TargetUserId = input.TargetUserId,
            ConversationId = conversation.Id,
            Type = ConversationType.User,
            ConversationName = null, // User conversation doesn't have name
            CreatorUserId = currentUserId,
            CreatorUserName = currentUser?.UserName ?? "",
            CreatorName = currentUser?.Name ?? "",
            CreatorSurname = currentUser?.Surname ?? ""
        });
        
        return await MapToConversationDtoAsync(conversation, currentUserId);
    }
    
    public virtual async Task<ConversationDto> CreateGroupConversationAsync(CreateGroupConversationInput input)
    {
        var currentUserId = CurrentUser.GetId();
        
        // Validate all member users exist
        var allUserIds = new List<Guid> { currentUserId };
        allUserIds.AddRange(input.MemberUserIds);
        
        await ValidateChatUsersExistAsync(allUserIds);
        
        Conversation conversation;
        using (var uow = UnitOfWorkManager.Begin(requiresNew: true))
        {
            // Create ONLY ONE conversation for the group (not per member)
            conversation = new Conversation(
                GuidGenerator.Create(),
                ConversationType.Group,
                input.Name,
                input.Description,
                null, // No project
                null, // No task
                CurrentTenant.Id
            );
            // Initialize LastMessage properties to avoid null constraint violation
            conversation.LastMessage = string.Empty;
            conversation.LastMessageDate = Clock.Now;
            await _conversationRepository.InsertAsync(conversation);
            
            // Add ALL members (including creator) to ConversationMember
            // Creator as ADMIN
            var creatorMember = new ConversationMember(
                GuidGenerator.Create(),
                conversation.Id,
                currentUserId,
                "ADMIN",
                CurrentTenant.Id
            );
            await _conversationMemberRepository.InsertAsync(creatorMember);
            
            // Add other members as MEMBER
            foreach (var userId in input.MemberUserIds.Where(id => id != currentUserId))
            {
                var member = new ConversationMember(
                    GuidGenerator.Create(),
                    conversation.Id,
                    userId,
                    "MEMBER",
                    CurrentTenant.Id
                );
                await _conversationMemberRepository.InsertAsync(member);
            }
            
            await uow.CompleteAsync();
        }
        
        // Publish event to notify all members about new conversation
        var currentUser = await _chatUserLookupService.FindByIdAsync(currentUserId);
        var allMemberIds = new List<Guid>(input.MemberUserIds);
        if (!allMemberIds.Contains(currentUserId))
        {
            allMemberIds.Add(currentUserId);
        }
        
        foreach (var memberId in allMemberIds)
        {
            await _distributedEventBus.PublishAsync(new ConversationCreatedEto
            {
                TargetUserId = memberId,
                ConversationId = conversation.Id,
                Type = ConversationType.Group,
                ConversationName = conversation.Name,
                CreatorUserId = currentUserId,
                CreatorUserName = currentUser?.UserName ?? "",
                CreatorName = currentUser?.Name ?? "",
                CreatorSurname = currentUser?.Surname ?? ""
            });
        }
        
        return await MapToConversationDtoAsync(conversation, currentUserId);
    }
    
    public virtual async Task<ConversationDto> CreateProjectConversationAsync(CreateProjectConversationInput input)
    {
        var currentUserId = CurrentUser.GetId();
        
        // Validate all member users exist
        var allUserIds = new List<Guid> { currentUserId };
        if (input.MemberUserIds != null && input.MemberUserIds.Any())
        {
            allUserIds.AddRange(input.MemberUserIds);
        }
        
        await ValidateChatUsersExistAsync(allUserIds);
        
        Conversation conversation;
        using (var uow = UnitOfWorkManager.Begin(requiresNew: true))
        {
            // Create ONLY ONE conversation for the project (not per member)
            conversation = new Conversation(
                GuidGenerator.Create(),
                ConversationType.Project,
                input.Name ?? $"Project {input.ProjectId}",
                null,
                input.ProjectId,
                null,
                CurrentTenant.Id
            );
            // Initialize LastMessage properties to avoid null constraint violation
            conversation.LastMessage = string.Empty;
            conversation.LastMessageDate = Clock.Now;
            await _conversationRepository.InsertAsync(conversation);
            
            // Add ALL members (including creator) to ConversationMember
            // Creator as ADMIN
            var creatorMember = new ConversationMember(
                GuidGenerator.Create(),
                conversation.Id,
                currentUserId,
                "ADMIN",
                CurrentTenant.Id
            );
            await _conversationMemberRepository.InsertAsync(creatorMember);
            
            // Add other members if provided
            if (input.MemberUserIds != null)
            {
                foreach (var userId in input.MemberUserIds.Where(id => id != currentUserId))
                {
                    var member = new ConversationMember(
                        GuidGenerator.Create(),
                        conversation.Id,
                        userId,
                        "MEMBER",
                        CurrentTenant.Id
                    );
                    await _conversationMemberRepository.InsertAsync(member);
                }
            }
            
            await uow.CompleteAsync();
        }
        
        // Publish event to notify all members about new conversation
        var currentUser = await _chatUserLookupService.FindByIdAsync(currentUserId);
        var allMemberIds = new List<Guid> { currentUserId };
        if (input.MemberUserIds != null && input.MemberUserIds.Any())
        {
            allMemberIds.AddRange(input.MemberUserIds.Where(id => id != currentUserId));
        }
        
        foreach (var memberId in allMemberIds)
        {
            await _distributedEventBus.PublishAsync(new ConversationCreatedEto
            {
                TargetUserId = memberId,
                ConversationId = conversation.Id,
                Type = ConversationType.Project,
                ConversationName = conversation.Name,
                CreatorUserId = currentUserId,
                CreatorUserName = currentUser?.UserName ?? "",
                CreatorName = currentUser?.Name ?? "",
                CreatorSurname = currentUser?.Surname ?? ""
            });
        }
        
        return await MapToConversationDtoAsync(conversation, currentUserId);
    }
    
    public virtual async Task<ConversationDto> CreateTaskConversationAsync(CreateTaskConversationInput input)
    {
        var currentUserId = CurrentUser.GetId();
        
        // Validate all member users exist
        var allUserIds = new List<Guid> { currentUserId };
        if (input.MemberUserIds != null && input.MemberUserIds.Any())
        {
            allUserIds.AddRange(input.MemberUserIds);
        }
        
        await ValidateChatUsersExistAsync(allUserIds);
        
        Conversation conversation;
        using (var uow = UnitOfWorkManager.Begin(requiresNew: true))
        {
            // Create ONLY ONE conversation for the task (not per member)
            conversation = new Conversation(
                GuidGenerator.Create(),
                ConversationType.Task,
                input.Name ?? $"Task {input.TaskId}",
                null,
                null,
                input.TaskId,
                CurrentTenant.Id
            );
            // Initialize LastMessage properties to avoid null constraint violation
            conversation.LastMessage = string.Empty;
            conversation.LastMessageDate = Clock.Now;
            await _conversationRepository.InsertAsync(conversation);
            
            // Add ALL members (including creator) to ConversationMember
            // Creator as ADMIN
            var creatorMember = new ConversationMember(
                GuidGenerator.Create(),
                conversation.Id,
                currentUserId,
                "ADMIN",
                CurrentTenant.Id
            );
            await _conversationMemberRepository.InsertAsync(creatorMember);
            
            // Add other members if provided
            if (input.MemberUserIds != null)
            {
                foreach (var userId in input.MemberUserIds.Where(id => id != currentUserId))
                {
                    var member = new ConversationMember(
                        GuidGenerator.Create(),
                        conversation.Id,
                        userId,
                        "MEMBER",
                        CurrentTenant.Id
                    );
                    await _conversationMemberRepository.InsertAsync(member);
                }
            }
            
            await uow.CompleteAsync();
        }
        
        // Publish event to notify all members about new conversation
        var currentUser = await _chatUserLookupService.FindByIdAsync(currentUserId);
        var allMemberIds = new List<Guid> { currentUserId };
        if (input.MemberUserIds != null && input.MemberUserIds.Any())
        {
            allMemberIds.AddRange(input.MemberUserIds.Where(id => id != currentUserId));
        }
        
        foreach (var memberId in allMemberIds)
        {
            await _distributedEventBus.PublishAsync(new ConversationCreatedEto
            {
                TargetUserId = memberId,
                ConversationId = conversation.Id,
                Type = ConversationType.Task,
                ConversationName = conversation.Name,
                CreatorUserId = currentUserId,
                CreatorUserName = currentUser?.UserName ?? "",
                CreatorName = currentUser?.Name ?? "",
                CreatorSurname = currentUser?.Surname ?? ""
            });
        }
        
        return await MapToConversationDtoAsync(conversation, currentUserId);
    }
    
    public virtual async Task<ConversationDto> UpdateConversationNameAsync(UpdateConversationNameInput input)
    {
        var conversation = await _conversationRepository.GetAsync(input.ConversationId);
        
        // Check if user is member
        var currentUserId = CurrentUser.GetId();
        var isMember = await _conversationRepository.IsUserMemberAsync(input.ConversationId, currentUserId);
        if (!isMember)
        {
            throw new BusinessException("HC.Chat:UserNotMember");
        }
        
        conversation.UpdateName(input.Name);
        await _conversationRepository.UpdateAsync(conversation);
        
        return await MapToConversationDtoAsync(conversation, currentUserId);
    }
    
    public virtual async Task PinConversationAsync(Guid conversationId)
    {
        var currentUserId = CurrentUser.GetId();
        var member = await _conversationMemberRepository.GetByConversationAndUserAsync(conversationId, currentUserId);
        
        if (member == null)
        {
            throw new BusinessException("HC.Chat:UserNotMember");
        }
        
        member.Pin();
        await _conversationMemberRepository.UpdateAsync(member);
    }
    
    public virtual async Task UnpinConversationAsync(Guid conversationId)
    {
        var currentUserId = CurrentUser.GetId();
        var member = await _conversationMemberRepository.GetByConversationAndUserAsync(conversationId, currentUserId);
        
        if (member == null)
        {
            throw new BusinessException("HC.Chat:UserNotMember");
        }
        
        member.Unpin();
        await _conversationMemberRepository.UpdateAsync(member);
    }
    
    public virtual async Task<string> AddMemberAsync(AddMemberInput input)
    {
        string errorMessage =string.Empty;
        try{
            var conversation = await _conversationRepository.GetWithMembersAsync(input.ConversationId);
            if (conversation == null)
            {
                return "HC.Chat:ConversationNotFound";
            }

            // var currentUserId = CurrentUser.GetId();
            
            // Check if current user is member and has permission (ADMIN only for now)
            // var currentMember = conversation.Members.FirstOrDefault(m => m.UserId == currentUserId && m.IsActive);
            // if (currentMember == null || currentMember.Role != "ADMIN")
            // {
            //     throw new BusinessException("HC.Chat:OnlyAdminCanAddMembers");
            // }

            
            using (var uow = UnitOfWorkManager.Begin(requiresNew: true))
            {
                foreach (var userId in input.UserIds)
                {
                    // Check if user already exists
                    var exists = await _conversationMemberRepository.ExistsAsync(input.ConversationId, userId);
                    if (exists)
                    {
                        // Reactivate if deactivated
                        var existingMember = await _conversationMemberRepository.GetByConversationAndUserAsync(input.ConversationId, userId);
                        if (existingMember != null && !existingMember.IsActive)
                        {
                            existingMember.Activate();
                            await _conversationMemberRepository.UpdateAsync(existingMember);
                            await BackfillConversationHistoryForMemberAsync(conversation, userId);
                        }
                        continue;
                    }
                    
                    // Validate user exists
                    var user = await _chatUserLookupService.FindByIdAsync(userId);
                    if (user == null)
                    {
                        throw new BusinessException("HC.Chat:UserNotFound").WithData("UserId", userId);
                    }
                    
                    // Add member
                    var member = new ConversationMember(
                        GuidGenerator.Create(),
                        input.ConversationId,
                        userId,
                        "MEMBER",
                        CurrentTenant.Id
                    );
                    await _conversationMemberRepository.InsertAsync(member);
                    await BackfillConversationHistoryForMemberAsync(conversation, userId);
                    
                }
                
                await uow.CompleteAsync();
            }
        }
        catch(Exception ex)
        {
            errorMessage = ex.Message;
        }   
        return errorMessage;
    }

    private async Task BackfillConversationHistoryForMemberAsync(Conversation conversation, Guid userId)
    {
        var messages = await _messageRepository.GetByConversationIdAsync(conversation.Id);
        if (messages.Count == 0)
        {
            return;
        }

        var existingMessageIds = await _userMessageRepository.GetMessageIdsByConversationIdAsync(conversation.Id, userId);
        var targetUserId = conversation.Members
            .Where(m => m.IsActive && m.UserId != userId)
            .Select(m => m.UserId)
            .FirstOrDefault();

        var missingUserMessages = new List<UserMessage>();
        foreach (var message in messages)
        {
            if (existingMessageIds.Contains(message.Id))
            {
                continue;
            }

            var side = message.CreatorId == userId ? ChatMessageSide.Sender : ChatMessageSide.Receiver;
            var userMessage = new UserMessage(
                GuidGenerator.Create(),
                userId,
                message.Id,
                side,
                targetUserId == Guid.Empty ? userId : targetUserId,
                CurrentTenant.Id);

            // Historical messages should be visible immediately without inflating unread counts.
            userMessage.MarkAsRead(Clock.Now);
            missingUserMessages.Add(userMessage);
        }

        if (missingUserMessages.Count > 0)
        {
            await _userMessageRepository.InsertManyAsync(missingUserMessages);
        }
    }
    
    public virtual async Task RemoveMemberAsync(RemoveMemberInput input)
    {
        var conversation = await _conversationRepository.GetWithMembersAsync(input.ConversationId);
        if (conversation == null)
        {
            throw new BusinessException("HC.Chat:ConversationNotFound");
        }
        
        var currentUserId = CurrentUser.GetId();
        var activeMembers = conversation.Members.Where(m => m.IsActive).ToList();
        
        var currentMember = activeMembers.FirstOrDefault(m => m.UserId == currentUserId);
        if (currentMember == null)
        {
            throw new BusinessException("HC.Chat:UserNotMember");
        }

        var isSelfLeave = input.UserId == currentUserId;
        
        if (!isSelfLeave && currentMember.Role != "ADMIN")
        {
            throw new BusinessException("HC.Chat:OnlyAdminCanRemoveMembers");
        }
        
        var memberToRemove = activeMembers.FirstOrDefault(m => m.UserId == input.UserId);
        if (memberToRemove == null)
        {
            throw new BusinessException("HC.Chat:MemberNotFound");
        }

        if (memberToRemove.Role == "ADMIN")
        {
            var adminCount = activeMembers.Count(m => m.Role == "ADMIN");
            if (adminCount <= 1)
            {
                var otherActiveMembers = activeMembers.Where(m => m.UserId != input.UserId).ToList();
                if (otherActiveMembers.Any())
                {
                    throw new BusinessException("HC.Chat:MustTransferAdminBeforeLeaving");
                }
            }
        }

        memberToRemove.Deactivate();
        await _conversationMemberRepository.UpdateAsync(memberToRemove);
    }
    
    public virtual async Task SetMemberRoleAsync(SetMemberRoleInput input)
    {
        var conversation = await _conversationRepository.GetWithMembersAsync(input.ConversationId);
        if (conversation == null)
        {
            throw new BusinessException("HC.Chat:ConversationNotFound");
        }
        
        var currentUserId = CurrentUser.GetId();
        var activeMembers = conversation.Members.Where(m => m.IsActive).ToList();
        
        var currentMember = activeMembers.FirstOrDefault(m => m.UserId == currentUserId);
        if (currentMember == null || currentMember.Role != "ADMIN")
        {
            throw new BusinessException("HC.Chat:OnlyAdminCanChangeRoles");
        }
        
        var targetMember = activeMembers.FirstOrDefault(m => m.UserId == input.UserId);
        if (targetMember == null)
        {
            throw new BusinessException("HC.Chat:MemberNotFound");
        }
        
        targetMember.SetRole(input.Role);
        await _conversationMemberRepository.UpdateAsync(targetMember);
    }

    public virtual async Task LeaveConversationAsync(LeaveConversationInput input)
    {
        var conversation = await _conversationRepository.GetWithMembersAsync(input.ConversationId);
        if (conversation == null)
        {
            throw new BusinessException("HC.Chat:ConversationNotFound");
        }

        var currentUserId = CurrentUser.GetId();
        var activeMembers = conversation.Members.Where(m => m.IsActive).ToList();

        var currentMember = activeMembers.FirstOrDefault(m => m.UserId == currentUserId);
        if (currentMember == null)
        {
            throw new BusinessException("HC.Chat:UserNotMember");
        }

        if (currentMember.Role == "ADMIN")
        {
            var adminCount = activeMembers.Count(m => m.Role == "ADMIN");
            var otherActiveMembers = activeMembers.Where(m => m.UserId != currentUserId).ToList();
            if (adminCount <= 1 && otherActiveMembers.Any())
            {
                throw new BusinessException("HC.Chat:MustTransferAdminBeforeLeaving");
            }
        }

        currentMember.Deactivate();
        await _conversationMemberRepository.UpdateAsync(currentMember);
    }

    public virtual async Task TransferAdminAndLeaveAsync(TransferAdminAndLeaveInput input)
    {
        var conversation = await _conversationRepository.GetWithMembersAsync(input.ConversationId);
        if (conversation == null)
        {
            throw new BusinessException("HC.Chat:ConversationNotFound");
        }

        var currentUserId = CurrentUser.GetId();
        var activeMembers = conversation.Members.Where(m => m.IsActive).ToList();

        var currentMember = activeMembers.FirstOrDefault(m => m.UserId == currentUserId);
        if (currentMember == null || currentMember.Role != "ADMIN")
        {
            throw new BusinessException("HC.Chat:OnlyAdminCanTransferRole");
        }

        var newAdmin = activeMembers.FirstOrDefault(m => m.UserId == input.NewAdminUserId);
        if (newAdmin == null)
        {
            throw new BusinessException("HC.Chat:MemberNotFound");
        }

        newAdmin.SetRole("ADMIN");
        await _conversationMemberRepository.UpdateAsync(newAdmin);

        currentMember.Deactivate();
        await _conversationMemberRepository.UpdateAsync(currentMember);
    }

    public virtual async Task<ConversationPermissionDto> GetMyPermissionsAsync(Guid conversationId)
    {
        var conversation = await _conversationRepository.GetWithMembersAsync(conversationId);
        if (conversation == null)
        {
            throw new BusinessException("HC.Chat:ConversationNotFound");
        }

        var currentUserId = CurrentUser.GetId();
        var activeMembers = conversation.Members.Where(m => m.IsActive).ToList();

        var currentMember = activeMembers.FirstOrDefault(m => m.UserId == currentUserId);
        if (currentMember == null)
        {
            throw new BusinessException("HC.Chat:UserNotMember");
        }

        var isAdmin = currentMember.Role == "ADMIN";
        var adminCount = activeMembers.Count(m => m.Role == "ADMIN");
        var isOnlyAdmin = isAdmin && adminCount <= 1;
        var hasOtherMembers = activeMembers.Count > 1;

        return new ConversationPermissionDto
        {
            MyRole = currentMember.Role,
            CanLeave = !isOnlyAdmin || !hasOtherMembers,
            CanDelete = isAdmin,
            CanAddMembers = isAdmin,
            CanRemoveMembers = isAdmin,
            CanChangeRoles = isAdmin,
            IsOnlyAdmin = isOnlyAdmin,
            AdminCount = adminCount,
            MemberCount = activeMembers.Count
        };
    }
    
    public virtual async Task<List<ConversationMemberDto>> GetMembersAsync(Guid conversationId)
    {
        var currentUserId = CurrentUser.GetId();
        
        // Check if user is member
        var isMember = await _conversationRepository.IsUserMemberAsync(conversationId, currentUserId);
        if (!isMember)
        {
            throw new BusinessException("HC.Chat:UserNotMember");
        }
        
        var members = await _conversationMemberRepository.GetByConversationIdAsync(conversationId);
        var result = new List<ConversationMemberDto>();
        
        foreach (var member in members.Where(m => m.IsActive))
        {
            var user = await _chatUserLookupService.FindByIdAsync(member.UserId);
            result.Add(new ConversationMemberDto
            {
                Id = member.Id,
                ConversationId = member.ConversationId,
                UserId = member.UserId,
                Role = member.Role,
                IsActive = member.IsActive,
                IsPinned = member.IsPinned,
                PinnedDate = member.PinnedDate,
                UnreadMessageCount = member.UnreadMessageCount,
                JoinedDate = member.JoinedDate,
                UserInfo = user != null ? new ChatTargetUserInfo
                {
                    UserId = user.Id,
                    Name = user.Name,
                    Surname = user.Surname,
                    Username = user.UserName
                } : null
            });
        }
        
        return result;
    }
    
    public virtual async Task<List<ConversationDto>> GetPinnedConversationsAsync()
    {
        var currentUserId = CurrentUser.GetId();
        var pinnedMembers = await _conversationMemberRepository.GetPinnedByUserIdAsync(currentUserId);
        
        var result = new List<ConversationDto>();
        foreach (var member in pinnedMembers)
        {
            var conversation = await _conversationRepository.GetAsync(member.ConversationId);
            if (conversation != null)
            {
                result.Add(await MapToConversationDtoAsync(conversation, currentUserId));
            }
        }
        
        return result.OrderByDescending(c => c.PinnedDate).ToList();
    }
    
    public virtual async Task<List<ConversationDto>> GetByTypeAsync(ConversationType type)
    {
        var currentUserId = CurrentUser.GetId();
        var conversations = await _conversationRepository.GetByTypeAsync(currentUserId, type);
        
        var result = new List<ConversationDto>();
        foreach (var conversation in conversations)
        {
            result.Add(await MapToConversationDtoAsync(conversation, currentUserId));
        }
        
        return result.OrderByDescending(c => c.LastMessageDate).ToList();
    }
    
    public virtual async Task<ChatMessageDto> SendReplyMessageAsync(SendReplyMessageInput input)
    {
        // Validate reply to message exists
        var replyToMessage = await _messageRepository.GetWithReplyAsync(input.ReplyToMessageId);
        if (replyToMessage == null)
        {
            throw new BusinessException("HC.Chat:MessageNotFound");
        }
        
        Message message;
        Guid targetUserId;
        string conversationType = ConversationType.User.ToString();
        string? conversationName = null;
        
        if (input.ConversationId.HasValue)
        {
            // Group/Project/Task conversation
            var conversation = await _conversationRepository.GetWithMembersAsync(input.ConversationId.Value);
            if (conversation == null)
            {
                throw new BusinessException("HC.Chat:ConversationNotFound");
            }
            
            var currentUserId = CurrentUser.GetId();
            var isMember = await _conversationRepository.IsUserMemberAsync(input.ConversationId.Value, currentUserId);
            if (!isMember)
            {
                throw new BusinessException("HC.Chat:UserNotMember");
            }
            
            // For group conversations, we need to create UserMessage for all members
            // For now, use first member as target (this needs to be improved)
            targetUserId = conversation.Members.FirstOrDefault(m => m.UserId != currentUserId && m.IsActive)?.UserId ?? currentUserId;
            conversationType = conversation.Type.ToString();
            conversationName = conversation.Name;
        }
        else
        {
            // Direct conversation
            targetUserId = input.TargetUserId;
            var targetUser = await _chatUserLookupService.FindByIdAsync(targetUserId);
            if (targetUser == null)
            {
                throw new BusinessException("HC.Chat:010002");
            }
        }
        
        using (var uow = UnitOfWorkManager.Begin(requiresNew: true))
        {
            var currentUserId = CurrentUser.GetId();
            
            if (input.ConversationId.HasValue)
            {
                // Group/Project/Task conversation - create message with ConversationId
                message = new Message(
                    GuidGenerator.Create(),
                    input.Message,
                    CurrentTenant.Id,
                    input.ConversationId.Value
                );
                message.SetReplyTo(input.ReplyToMessageId);
                await _messageRepository.InsertAsync(message);
                
                // Create UserMessage for all active members
                var conversation = await _conversationRepository.GetWithMembersAsync(input.ConversationId.Value);
                var activeMembers = conversation.Members.Where(m => m.IsActive).ToList();
                
                foreach (var member in activeMembers)
                {
                    var side = member.UserId == currentUserId ? ChatMessageSide.Sender : ChatMessageSide.Receiver;
                    var targetId = activeMembers.FirstOrDefault(m => m.UserId != currentUserId)?.UserId ?? currentUserId;
                    
                    await _userMessageRepository.InsertAsync(
                        new UserMessage(GuidGenerator.Create(), member.UserId, message.Id, side, targetId, CurrentTenant.Id)
                    );
                }
                
                // Update LastMessage for the group conversation
                // Now there's only ONE conversation shared by all members
                var now = Clock.Now;
                var mainConversation = await _conversationRepository.GetAsync(input.ConversationId.Value);
                if (mainConversation != null)
                {
                    // Update the single conversation (shared by all members)
                    mainConversation.SetLastMessage(input.Message, now);
                    await _conversationRepository.UpdateAsync(mainConversation);
                }
            }
            else
            {
                // Direct conversation - create message without ConversationId
                message = new Message(
                    GuidGenerator.Create(),
                    input.Message,
                    CurrentTenant.Id,
                    null // Direct conversations don't have ConversationId
                );
                message.SetReplyTo(input.ReplyToMessageId);
                await _messageRepository.InsertAsync(message);
                
                // Create UserMessage entries
                await _userMessageRepository.InsertAsync(
                    new UserMessage(GuidGenerator.Create(), currentUserId, message.Id, ChatMessageSide.Sender, targetUserId, CurrentTenant.Id)
                );
                
                await _userMessageRepository.InsertAsync(
                    new UserMessage(GuidGenerator.Create(), targetUserId, message.Id, ChatMessageSide.Receiver, currentUserId, CurrentTenant.Id)
                );
                
                // Update conversation last message
                var conversationPair = await _conversationRepository.FindPairAsync(currentUserId, targetUserId);
                if (conversationPair != null)
                {
                    conversationPair.SenderConversation?.SetLastMessage(input.Message, Clock.Now);
                    conversationPair.TargetConversation?.SetLastMessage(input.Message, Clock.Now);
                    
                    if (conversationPair.SenderConversation != null)
                    {
                        await _conversationRepository.UpdateAsync(conversationPair.SenderConversation);
                    }
                    if (conversationPair.TargetConversation != null)
                    {
                        await _conversationRepository.UpdateAsync(conversationPair.TargetConversation);
                    }
                }
            }
            
            await uow.CompleteAsync();
        }
        
        // Send real-time notification
        var senderUser = await _chatUserLookupService.FindByIdAsync(CurrentUser.GetId());
        var notifyConversationName = conversationName;
        if (input.ConversationId.HasValue && conversationType == ConversationType.User.ToString())
        {
            notifyConversationName = FormatUserConversationDisplayName(senderUser) ?? conversationName;
        }

        await _realTimeChatMessageSender.SendAsync(
            targetUserId,
            new ChatMessageRdto
            {
                Id = message.Id,
                ConversationId = input.ConversationId,
                ConversationType = conversationType,
                ConversationName = notifyConversationName,
                SenderName = senderUser.Name,
                SenderSurname = senderUser.Surname,
                SenderUserId = senderUser.Id,
                SenderUsername = senderUser.UserName,
                Text = input.Message
            }
        );
        
        return await MapToChatMessageDtoAsync(message, ChatMessageSide.Sender, message.CreatorId);
    }
    
    public virtual async Task PinMessageAsync(Guid messageId)
    {
        var message = await _messageRepository.GetAsync(messageId);
        if (message == null)
        {
            throw new BusinessException("HC.Chat:MessageNotFound");
        }
        
        var currentUserId = CurrentUser.GetId();
        message.Pin(currentUserId);
        await _messageRepository.UpdateAsync(message);
    }
    
    public virtual async Task UnpinMessageAsync(Guid messageId)
    {
        var message = await _messageRepository.GetAsync(messageId);
        if (message == null)
        {
            throw new BusinessException("HC.Chat:MessageNotFound");
        }
        
        message.Unpin();
        await _messageRepository.UpdateAsync(message);
    }
    
    public virtual async Task<List<ChatMessageDto>> GetPinnedMessagesAsync(Guid conversationId)
    {
        var currentUserId = CurrentUser.GetId();
        
        // Check if user is member
        var isMember = await _conversationRepository.IsUserMemberAsync(conversationId, currentUserId);
        if (!isMember)
        {
            throw new BusinessException("HC.Chat:UserNotMember");
        }
        
        var pinnedMessages = await _messageRepository.GetPinnedMessagesAsync(conversationId);
        
        var senderSides = pinnedMessages.ConvertAll(_ => ChatMessageSide.Sender);
        return await MapToChatMessageDtosBatchAsync(pinnedMessages, senderSides, senderUserIdOverrides: null);
    }
    
    public virtual async Task<ChatMessageDto> SendMessageWithFilesAsync(SendMessageWithFilesInput input)
    {
        // First create the message
        Message message;
        Guid targetUserId;
        string conversationType = ConversationType.User.ToString();
        string? conversationName = null;
        
        if (input.ConversationId.HasValue)
        {
            var conversation = await _conversationRepository.GetWithMembersAsync(input.ConversationId.Value);
            if (conversation == null)
            {
                throw new BusinessException("HC.Chat:ConversationNotFound");
            }
            
            var currentUserId = CurrentUser.GetId();
            var isMember = await _conversationRepository.IsUserMemberAsync(input.ConversationId.Value, currentUserId);
            if (!isMember)
            {
                throw new BusinessException("HC.Chat:UserNotMember");
            }
            
            targetUserId = conversation.Members.FirstOrDefault(m => m.UserId != currentUserId && m.IsActive)?.UserId ?? currentUserId;
            conversationType = conversation.Type.ToString();
            conversationName = conversation.Name;
        }
        else
        {
            targetUserId = input.TargetUserId;
        }
        
        using (var uow = UnitOfWorkManager.Begin(requiresNew: true))
        {
            var currentUserId = CurrentUser.GetId();
            
            // When sending files without text, use a file attachment placeholder
            var messageText = !string.IsNullOrWhiteSpace(input.Message)
                ? input.Message
                : "📎";

            if (input.ConversationId.HasValue)
            {
                message = new Message(
                    GuidGenerator.Create(),
                    messageText,
                    CurrentTenant.Id,
                    input.ConversationId.Value
                );
                await _messageRepository.InsertAsync(message);
                
                var conversation = await _conversationRepository.GetWithMembersAsync(input.ConversationId.Value);
                var activeMembers = conversation.Members.Where(m => m.IsActive).ToList();
                
                foreach (var member in activeMembers)
                {
                    var side = member.UserId == currentUserId ? ChatMessageSide.Sender : ChatMessageSide.Receiver;
                    var targetId = activeMembers.FirstOrDefault(m => m.UserId != currentUserId)?.UserId ?? currentUserId;
                    
                    await _userMessageRepository.InsertAsync(
                        new UserMessage(GuidGenerator.Create(), member.UserId, message.Id, side, targetId, CurrentTenant.Id)
                    );
                }
                
                var now = Clock.Now;
                var mainConversation = await _conversationRepository.GetAsync(input.ConversationId.Value);
                if (mainConversation != null)
                {
                    mainConversation.SetLastMessage(messageText, now);
                    await _conversationRepository.UpdateAsync(mainConversation);
                }
            }
            else
            {
                message = await _messagingManager.CreateNewMessage(
                    currentUserId,
                    targetUserId,
                    messageText
                );
            }
            
            // Link files to message if provided
            if (input.FileIds != null && input.FileIds.Any())
            {
                foreach (var fileId in input.FileIds)
                {
                    var file = await _messageFileRepository.GetAsync(fileId);
                    if (file != null && !file.MessageId.HasValue) // Pre-uploaded file
                    {
                        // Update file with message ID
                        file.SetMessageId(message.Id);
                        await _messageFileRepository.UpdateAsync(file);
                    }
                }
            }
            
            await uow.CompleteAsync();
        }
        
        // Send real-time notification
        var senderUser = await _chatUserLookupService.FindByIdAsync(CurrentUser.GetId());
        var notifyConversationNameWithFiles = conversationName;
        if (input.ConversationId.HasValue && conversationType == ConversationType.User.ToString())
        {
            notifyConversationNameWithFiles = FormatUserConversationDisplayName(senderUser) ?? conversationName;
        }

        await _realTimeChatMessageSender.SendAsync(
            targetUserId,
            new ChatMessageRdto
            {
                Id = message.Id,
                ConversationId = input.ConversationId,
                ConversationType = conversationType,
                ConversationName = notifyConversationNameWithFiles,
                SenderName = senderUser.Name,
                SenderSurname = senderUser.Surname,
                SenderUserId = senderUser.Id,
                SenderUsername = senderUser.UserName,
                Text = input.Message
            }
        );
        
        return await MapToChatMessageDtoAsync(message, ChatMessageSide.Sender, message.CreatorId);
    }
    
    public virtual async Task<MessageFileDto> UploadFileAsync(UploadFileInput input)
    {
        if (input.FileContent == null || input.FileContent.Length == 0)
        {
            throw new BusinessException("HC.Chat:FileContentRequired");
        }
        
        if (string.IsNullOrWhiteSpace(input.FileName))
        {
            throw new BusinessException("HC.Chat:FileNameRequired");
        }
        
        // Validate file size
        if (input.FileContent.Length > ChatConsts.MaxFileSize)
        {
            throw new BusinessException("HC.Chat:FileSizeExceeded")
                .WithData("MaxSize", ChatConsts.MaxFileSize)
                .WithData("ActualSize", input.FileContent.Length);
        }
        
        var currentUserId = CurrentUser.GetId();
        var tenantId = CurrentTenant.Id;
        
        // Generate file path: chat-files/{TenantId}/{ConversationId}/{MessageId}/{FileName}
        // For pre-upload, ConversationId and MessageId can be empty (will be updated later)
        var conversationIdStr = input.ConversationId?.ToString() ?? "temp";
        var messageIdStr = "temp";
        var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(input.FileName)}";
        var filePath = $"chat-files/{tenantId}/{conversationIdStr}/{messageIdStr}/{fileName}";
        
        // Upload to MINIO
        await _blobContainer.SaveAsync(filePath, input.FileContent);
        
        // Get file extension
        var fileExtension = Path.GetExtension(input.FileName).TrimStart('.');
        
        MessageFile messageFile;
        using (var uow = UnitOfWorkManager.Begin(requiresNew: true))
        {
            // Create MessageFile entity (MessageId will be set later when message is created)
            // Use null for pre-uploaded files
            messageFile = new MessageFile(
                GuidGenerator.Create(),
                null, // Will be set when message is created via SetMessageId()
                input.FileName,
                filePath,
                input.ContentType ?? "application/octet-stream",
                input.FileContent.Length,
                fileExtension,
                currentUserId,
                tenantId
            );
            
            await _messageFileRepository.InsertAsync(messageFile);
            await uow.CompleteAsync();
        }
        
        return new MessageFileDto
        {
            Id = messageFile.Id,
            MessageId = messageFile.MessageId,
            FileName = messageFile.FileName,
            ContentType = messageFile.ContentType,
            FileSize = messageFile.FileSize,
            FileExtension = messageFile.FileExtension,
            DownloadUrl = $"/api/chat/files/{messageFile.Id}/download", // TODO: Generate signed URL
            CreationTime = messageFile.CreationTime
        };
    }
    
    public virtual async Task<FileDto> DownloadFileAsync(Guid fileId)
    {
        var file = await _messageFileRepository.GetWithMessageAsync(fileId);
        if (file == null)
        {
            throw new BusinessException("HC.Chat:FileNotFound");
        }
        
        // Check if user has access to the message
        if (!file.MessageId.HasValue)
        {
            throw new BusinessException("HC.Chat:FileNotAttachedToMessage");
        }
        
        var currentUserId = CurrentUser.GetId();
        var userMessages = await _userMessageRepository.GetListAsync(file.MessageId.Value);
        var hasAccess = userMessages.Any(um => um.UserId == currentUserId);
        
        if (!hasAccess)
        {
            throw new BusinessException("HC.Chat:FileAccessDenied");
        }
        
        // Download from MINIO
        var fileBytes = await _blobContainer.GetAllBytesAsync(file.FilePath);
        
        return new FileDto
        {
            Content = fileBytes,
            FileName = file.FileName,
            ContentType = file.ContentType
        };
    }
    
    public virtual async Task DeleteFileAsync(Guid fileId)
    {
        var file = await _messageFileRepository.GetWithMessageAsync(fileId);
        if (file == null)
        {
            throw new BusinessException("HC.Chat:FileNotFound");
        }
        
        // Check if user has access
        if (!file.MessageId.HasValue)
        {
            throw new BusinessException("HC.Chat:FileNotAttachedToMessage");
        }
        
        var currentUserId = CurrentUser.GetId();
        var userMessages = await _userMessageRepository.GetListAsync(file.MessageId.Value);
        var hasAccess = userMessages.Any(um => um.UserId == currentUserId);
        
        if (!hasAccess)
        {
            throw new BusinessException("HC.Chat:FileAccessDenied");
        }
        
        using (var uow = UnitOfWorkManager.Begin(requiresNew: true))
        {
            // Delete from MINIO
            await _blobContainer.DeleteAsync(file.FilePath);
            
            // Delete from database
            await _messageFileRepository.DeleteAsync(file);

            await uow.CompleteAsync();
        }
    }

    public virtual async Task<List<MessageFileDto>> GetMessageFilesAsync(Guid messageId)
    {
        var currentUserId = CurrentUser.GetId();
        var userMessages = await _userMessageRepository.GetListAsync(messageId);
        var hasAccess = userMessages.Any(um => um.UserId == currentUserId);

        if (!hasAccess)
        {
            throw new BusinessException("HC.Chat:FileAccessDenied");
        }

        var files = await _messageFileRepository.GetByMessageIdAsync(messageId);

        return files.Select(file => new MessageFileDto
        {
            Id = file.Id,
            MessageId = file.MessageId,
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSize = file.FileSize,
            FileExtension = file.FileExtension,
            FilePath = file.FilePath,
            DownloadUrl = $"/api/chat/files/{file.Id}/download",
            CreationTime = file.CreationTime
        }).ToList();
    }

    /// <summary>
    /// Forward a message to another conversation
    /// </summary>
    public virtual async Task<ChatMessageDto> ForwardMessageAsync(ForwardMessageInput input)
    {
        var currentUserId = CurrentUser.GetId();
        
        // Get the original message
        var originalMessage = await _messageRepository.GetWithReplyAsync(input.MessageId);
        if (originalMessage == null)
        {
            throw new BusinessException("HC.Chat:MessageNotFound");
        }
        
        // Check target conversation exists and user is member
        var targetConversation = await _conversationRepository.GetWithMembersAsync(input.TargetConversationId);
        if (targetConversation == null)
        {
            throw new BusinessException("HC.Chat:ConversationNotFound");
        }
        
        var isMember = await _conversationRepository.IsUserMemberAsync(input.TargetConversationId, currentUserId);
        if (!isMember)
        {
            throw new BusinessException("HC.Chat:UserNotMember");
        }
        
        // Optional user comment only; do not duplicate original text (UI shows it on ForwardedFromMessage).
        var forwardedText = !string.IsNullOrWhiteSpace(input.AdditionalComment)
            ? input.AdditionalComment
            : ForwardedMessageEmptyCommentPlaceholder;
        
        Message newMessage;
        List<Guid> memberUserIds;
        
        using (var uow = UnitOfWorkManager.Begin(requiresNew: true))
        {
            // Create forwarded message
            newMessage = new Message(
                GuidGenerator.Create(),
                forwardedText,
                CurrentTenant.Id,
                input.TargetConversationId
            );
            
            // Mark as forwarded message
            newMessage.SetForwardedFrom(input.MessageId);
            
            await _messageRepository.InsertAsync(newMessage);
            
            // Create UserMessage for all active members
            var activeMembers = targetConversation.Members.Where(m => m.IsActive).ToList();
            memberUserIds = activeMembers.Select(m => m.UserId).ToList();
            
            foreach (var member in activeMembers)
            {
                var side = member.UserId == currentUserId ? ChatMessageSide.Sender : ChatMessageSide.Receiver;
                var targetId = activeMembers.FirstOrDefault(m => m.UserId != currentUserId)?.UserId ?? currentUserId;
                
                await _userMessageRepository.InsertAsync(
                    new UserMessage(GuidGenerator.Create(), member.UserId, newMessage.Id, side, targetId, CurrentTenant.Id)
                );
            }
            
            // Update LastMessage for the conversation
            var now = Clock.Now;
            targetConversation.SetLastMessage(forwardedText.Length > 50 ? forwardedText.Substring(0, 50) + "..." : forwardedText, now);
            await _conversationRepository.UpdateAsync(targetConversation);
            
            await uow.CompleteAsync();
        }
        
        // Send real-time notification to all members except sender
        var senderUser = await _chatUserLookupService.FindByIdAsync(currentUserId);
        if (senderUser != null)
        {
            var forwardConversationLabel = targetConversation.Type == ConversationType.User
                ? (FormatUserConversationDisplayName(senderUser) ?? targetConversation.Name)
                : targetConversation.Name;

            var messageDto = new ChatMessageRdto
            {
                Id = newMessage.Id,
                ConversationId = input.TargetConversationId,
                ConversationType = targetConversation.Type.ToString(),
                ConversationName = forwardConversationLabel,
                SenderName = senderUser.Name,
                SenderSurname = senderUser.Surname,
                SenderUserId = senderUser.Id,
                SenderUsername = senderUser.UserName,
                Text = forwardedText
            };
            
            var recipientUserIds = memberUserIds.Where(id => id != currentUserId).ToList();
            foreach (var recipientUserId in recipientUserIds)
            {
                await _realTimeChatMessageSender.SendAsync(recipientUserId, messageDto);
            }
        }
        
        return await MapToChatMessageDtoAsync(newMessage, ChatMessageSide.Sender, currentUserId);
    }



    public virtual async Task<ConversationDto> FindConversationAsync(FindConversationInput input)
    {
        var members = await _conversationMemberRepository.GetByUserIdsAsync(input.UserIds, input.Type);
        if (members.Count == 0)
        {
            return null;
        }

        var currentUserId = CurrentUser.GetId();

        return await MapToConversationDtoAsync(members.First().Conversation, currentUserId);
    }


    
    public virtual async Task<ConversationDto> FindConversationByProjectIdAsync(Guid projectId)
    {
        var conversation = await _conversationRepository.GetByProjectIdAsync(projectId);
        if (conversation == null || conversation.Count == 0)
        {
            return null;
        }
        return await MapToConversationDtoAsync(conversation.First(), CurrentUser.GetId());
    }


    public virtual async Task<List<ChatMessageDto>> FindMessagesInConversationAsync(FindMessageInConversationInput input)
    {
        var messages = await _messageRepository.GetMessagesInConversationAsync(
            conversationId: input.ConversationId,
            messageText: input.MessageText,
            maxResultCount: input.MaxResultCount,
            skipCount: input.SkipCount
        );
        List<ChatMessageDto> messDto = new ();
        var sides = messages.ConvertAll(_ => ChatMessageSide.Sender);
        messDto.AddRange(await MapToChatMessageDtosBatchAsync(messages, sides, senderUserIdOverrides: null));
        return messDto;
    }

    public virtual async Task<MessageContextDto> GetMessageContextAsync(GetMessageContextInput input)
    {
        var currentUserId = CurrentUser.GetId();
        var isMember = await _conversationRepository.IsUserMemberAsync(input.ConversationId, currentUserId);
        if (!isMember)
        {
            throw new BusinessException("HC.Chat:UserNotMember");
        }

        var context = await _messageRepository.GetMessageContextAsync(
            input.ConversationId,
            input.MessageId,
            input.BeforeCount,
            input.AfterCount);

        var result = new MessageContextDto
        {
            HasMoreBefore = context.HasMoreBefore,
            HasMoreAfter = context.HasMoreAfter
        };

        var contextOverride = Enumerable.Repeat((Guid?)currentUserId, 1).ToList();
        if (context.Anchor != null)
        {
            result.AnchorMessage = (await MapToChatMessageDtosBatchAsync(
                new List<Message> { context.Anchor },
                new List<ChatMessageSide>
                {
                    context.Anchor.CreatorId == currentUserId ? ChatMessageSide.Sender : ChatMessageSide.Receiver
                },
                contextOverride))[0];
        }

        var beforeSides = context.Before.ConvertAll(m =>
            m.CreatorId == currentUserId ? ChatMessageSide.Sender : ChatMessageSide.Receiver);
        var beforeOverrides = Enumerable.Repeat((Guid?)currentUserId, context.Before.Count).ToList();
        result.BeforeMessages.AddRange(await MapToChatMessageDtosBatchAsync(context.Before, beforeSides, beforeOverrides));

        var afterSides = context.After.ConvertAll(m =>
            m.CreatorId == currentUserId ? ChatMessageSide.Sender : ChatMessageSide.Receiver);
        var afterOverrides = Enumerable.Repeat((Guid?)currentUserId, context.After.Count).ToList();
        result.AfterMessages.AddRange(await MapToChatMessageDtosBatchAsync(context.After, afterSides, afterOverrides));

        return result;
    }

    public virtual async Task<List<MessageSearchResultDto>> SearchMessagesAsync(SearchConversationMessagesInput input)
    {
        var currentUserId = CurrentUser.GetId();
        var isMember = await _conversationRepository.IsUserMemberAsync(input.ConversationId, currentUserId);
        if (!isMember)
        {
            throw new BusinessException("HC.Chat:UserNotMember");
        }

        var hits = await _messageRepository.SearchInConversationAsync(
            input.ConversationId,
            input.Keyword,
            input.MaxResultCount,
            input.SkipCount);

        return hits.Select(x => new MessageSearchResultDto
        {
            MessageId = x.MessageId,
            ConversationId = x.ConversationId,
            CreationTime = x.CreationTime,
            Snippet = x.Snippet.TruncateWithPostfix(120, "...")
        }).ToList();
    }



    public virtual async Task<List<MessageFileDto>> FindMediaAndFileInConversationAsync(FindMediaAndFileInConversationInput input)
    {
        var files = await _messageFileRepository.GetByConversationIdAndFileTypeAsync(
            input.ConversationId,input.FileType,
            fileName: input.FileName,
            maxResultCount: input.MaxResultCount,
            skipCount: input.SkipCount
        );
        List<MessageFileDto> fileDto = new ();
        foreach (var f in files)
        {
            fileDto.Add(new MessageFileDto
            {
                Id = f.Id,
                MessageId = f.MessageId,
                FileName = f.FileName,
                ContentType = f.ContentType,
                FilePath = f.FilePath,
                FileSize = f.FileSize,
                FileExtension = f.FileExtension,
            });
        }
        return fileDto;
    }

    public virtual async Task UpdateUnreadCountAsync(UpdateUnreadCountInput input)
    {
        var currentUserId = CurrentUser.GetId();
        var member = await _conversationMemberRepository.GetByConversationAndUserAsync(input.ConversationId, currentUserId);
        
        if (member == null)
        {
            throw new BusinessException("HC.Chat:UserNotMember");
        }
        
        if (input.IncrementBy > 0)
        {
            member.IncrementUnreadCount();
        }
        else if (input.IncrementBy < 0)
        {
            member.DecrementUnreadCount(Math.Abs(input.IncrementBy));
        }
        
        await _conversationMemberRepository.UpdateAsync(member);
    }
    
    public virtual async Task ResetUnreadCountAsync(ResetUnreadCountInput input)
    {
        var currentUserId = CurrentUser.GetId();
        var member = await _conversationMemberRepository.GetByConversationAndUserAsync(input.ConversationId, currentUserId);
        
        if (member == null)
        {
            throw new BusinessException("HC.Chat:UserNotMember");
        }
        
        member.ResetUnreadCount();
        await _conversationMemberRepository.UpdateAsync(member);
    }
    
    public virtual async Task<TotalUnreadCountDto> GetTotalUnreadCountAsync()
    {
        var currentUserId = CurrentUser.GetId();
        var allMembers = await _conversationMemberRepository.GetByUserIdAsync(currentUserId);
        
        var totalUnreadCount = allMembers
            .Where(m => m.IsActive)
            .Sum(m => m.UnreadMessageCount);
        
        return new TotalUnreadCountDto
        {
            TotalUnreadCount = totalUnreadCount
        };
    }
    
    #region helpers
    // Helper methods
    private async Task<ConversationDto> MapToConversationDtoAsync(Conversation conversation, Guid currentUserId)
    {
        var member = await _conversationMemberRepository.GetByConversationAndUserAsync(conversation.Id, currentUserId);
        var members = await _conversationMemberRepository.GetByConversationIdAsync(conversation.Id);
        
        var dto = new ConversationDto
        {
            Id = conversation.Id,
            Type = conversation.Type,
            Name = conversation.Name,
            Description = conversation.Description,
            IsPinned = member?.IsPinned ?? false,
            PinnedDate = member?.PinnedDate,
            ProjectId = conversation.ProjectId,
            TaskId = conversation.TaskId,
            MemberCount = members.Count(m => m.IsActive),
            LastMessage = conversation.LastMessage,
            LastMessageDate = conversation.LastMessageDate,
            UnreadMessageCount = member?.UnreadMessageCount ?? 0
        };
        
        // For User type, get target user info (from members, not TargetUserId)
        if (conversation.Type == ConversationType.User)
        {
            var targetMember = members.FirstOrDefault(m => m.UserId != currentUserId && m.IsActive);
            if (targetMember != null)
            {
                var targetUser = await _chatUserLookupService.FindByIdAsync(targetMember.UserId);
                if (targetUser != null)
                {
                    dto.TargetUserInfo = new ChatTargetUserInfo
                    {
                        UserId = targetUser.Id,
                        Name = targetUser.Name,
                        Surname = targetUser.Surname,
                        Username = targetUser.UserName
                    };
                    dto.Name = FormatUserConversationDisplayName(targetUser.Name, targetUser.Surname, targetUser.UserName);
                }
            }
        }
        else
        {
            // For Group/Project/Task, get members
            dto.Members = new List<ConversationMemberDto>();
            foreach (var m in members.Where(x => x.IsActive))
            {
                var user = await _chatUserLookupService.FindByIdAsync(m.UserId);
                dto.Members.Add(new ConversationMemberDto
                {
                    Id = m.Id,
                    ConversationId = m.ConversationId,
                    UserId = m.UserId,
                    Role = m.Role,
                    IsActive = m.IsActive,
                    IsPinned = m.IsPinned,
                    PinnedDate = m.PinnedDate,
                    UnreadMessageCount = m.UnreadMessageCount,
                    JoinedDate = m.JoinedDate,
                    UserInfo = user != null ? new ChatTargetUserInfo
                    {
                        UserId = user.Id,
                        Name = user.Name,
                        Surname = user.Surname,
                        Username = user.UserName
                    } : null
                });
            }
        }
        
        return dto;
    }
    
    private async Task<List<ChatMessageDto>> MapToChatMessageDtosBatchAsync(
        IReadOnlyList<Message> messages,
        IReadOnlyList<ChatMessageSide> sides,
        IReadOnlyList<Guid?>? senderUserIdOverrides)
    {
        var currentUserId = CurrentUser.GetId();
        var count = messages.Count;
        if (count == 0)
        {
            return new List<ChatMessageDto>();
        }

        if (sides.Count != count)
        {
            throw new ArgumentException("sides must have the same length as messages.", nameof(sides));
        }

        if (senderUserIdOverrides != null && senderUserIdOverrides.Count != count)
        {
            throw new ArgumentException("senderUserIdOverrides must have the same length as messages.", nameof(senderUserIdOverrides));
        }

        var senderIds = new HashSet<Guid>();
        var refIdSet = new HashSet<Guid>();
        var messageIds = new List<Guid>(count);

        for (var i = 0; i < count; i++)
        {
            var m = messages[i];
            messageIds.Add(m.Id);

            var effectiveSender = senderUserIdOverrides != null ? senderUserIdOverrides[i] : m.CreatorId;
            if (effectiveSender.HasValue)
            {
                senderIds.Add(effectiveSender.Value);
            }

            if (m.ReplyToMessageId.HasValue)
            {
                refIdSet.Add(m.ReplyToMessageId.Value);
            }

            if (m.ForwardedFromMessageId.HasValue)
            {
                refIdSet.Add(m.ForwardedFromMessageId.Value);
            }
        }

        var refMessages = refIdSet.Count > 0
            ? await _messageRepository.GetListByIdsAsync(refIdSet.ToList())
            : new List<Message>();
        var refById = refMessages.ToDictionary(x => x.Id);

        foreach (var rm in refMessages)
        {
            if (rm.CreatorId.HasValue)
            {
                senderIds.Add(rm.CreatorId.Value);
            }
        }

        var users = senderIds.Count > 0
            ? await _chatUserLookupService.GetListByIdsAsync(senderIds)
            : Array.Empty<ChatUser>();
        var userById = users.ToDictionary(u => u.Id);

        var allFiles = await _messageFileRepository.GetListByMessageIdsAsync(messageIds);
        var filesByMessageId = allFiles
            .Where(f => f.MessageId.HasValue)
            .GroupBy(f => f.MessageId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<ChatMessageDto>(count);
        for (var i = 0; i < count; i++)
        {
            result.Add(BuildChatMessageDtoFromCaches(
                messages[i],
                sides[i],
                senderUserIdOverrides?[i],
                currentUserId,
                refById,
                userById,
                filesByMessageId));
        }

        return result;
    }

    private static ChatMessageDto BuildChatMessageDtoFromCaches(
        Message message,
        ChatMessageSide side,
        Guid? senderUserIdOverride,
        Guid currentUserId,
        IReadOnlyDictionary<Guid, Message> refById,
        IReadOnlyDictionary<Guid, ChatUser> userById,
        IReadOnlyDictionary<Guid, List<MessageFile>> filesByMessageId)
    {
        var dto = new ChatMessageDto
        {
            Id = message.Id,
            Message = message.Text,
            MessageDate = message.CreationTime,
            ReadDate = message.ReadTime ?? DateTime.MaxValue,
            IsRead = message.IsAllRead,
            Side = side,
            IsPinned = message.IsPinned,
            PinnedDate = message.PinnedDate,
            ReplyToMessageId = message.ReplyToMessageId,
            SenderUserId = message.CreatorId,
            Files = new List<MessageFileDto>()
        };

        var effectiveSenderId = senderUserIdOverride ?? message.CreatorId;
        if (effectiveSenderId.HasValue && userById.TryGetValue(effectiveSenderId.Value, out var senderUser))
        {
            dto.SenderUserId = senderUser.Id;
            dto.SenderName = senderUser.Name;
            dto.SenderSurname = senderUser.Surname;
            dto.SenderUsername = senderUser.UserName;
        }

        if (message.ReplyToMessageId.HasValue &&
            refById.TryGetValue(message.ReplyToMessageId.Value, out var replyTo))
        {
            dto.ReplyToMessage = new ChatMessageDto
            {
                Id = replyTo.Id,
                Message = replyTo.Text,
                MessageDate = replyTo.CreationTime,
                IsPinned = replyTo.IsPinned,
                Side = replyTo.CreatorId == currentUserId ? ChatMessageSide.Sender : ChatMessageSide.Receiver
            };
        }

        if (message.ForwardedFromMessageId.HasValue &&
            refById.TryGetValue(message.ForwardedFromMessageId.Value, out var forwardedFrom))
        {
            ChatUser? forwardedSenderUser = null;
            if (forwardedFrom.CreatorId.HasValue)
            {
                userById.TryGetValue(forwardedFrom.CreatorId.Value, out forwardedSenderUser);
            }

            dto.ForwardedFromMessage = new ChatMessageDto
            {
                Id = forwardedFrom.Id,
                Message = forwardedFrom.Text,
                MessageDate = forwardedFrom.CreationTime,
                Side = forwardedFrom.CreatorId == currentUserId ? ChatMessageSide.Sender : ChatMessageSide.Receiver,
                SenderUserId = forwardedSenderUser?.Id,
                SenderName = forwardedSenderUser?.Name,
                SenderSurname = forwardedSenderUser?.Surname,
                SenderUsername = forwardedSenderUser?.UserName
            };
        }

        if (filesByMessageId.TryGetValue(message.Id, out var files))
        {
            foreach (var file in files)
            {
                dto.Files.Add(new MessageFileDto
                {
                    Id = file.Id,
                    MessageId = file.MessageId,
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    FileSize = file.FileSize,
                    FilePath = file.FilePath,
                    FileExtension = file.FileExtension,
                    DownloadUrl = $"/api/chat/files/{file.Id}/download",
                    CreationTime = file.CreationTime
                });
            }
        }

        return dto;
    }

    private async Task<ChatMessageDto> MapToChatMessageDtoAsync(Message message, ChatMessageSide side, Guid? senderUserId = null)
    {
        IReadOnlyList<Guid?>? overrides = null;
        if (senderUserId.HasValue)
        {
            overrides = new List<Guid?> { senderUserId.Value };
        }

        var batch = await MapToChatMessageDtosBatchAsync(
            new List<Message> { message },
            new List<ChatMessageSide> { side },
            overrides);
        return batch[0];
    }
    
    #endregion
}
