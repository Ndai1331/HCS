using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp.Features;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Users;
using HC.Chat.Authorization;
using HC.Chat.Conversations;
using HC.Chat;
using HC.Chat.Messages;

namespace HC.Chat.Users;

[RequiresFeature(ChatFeatures.Enable)]
[Authorize(ChatPermissions.Messaging)]
public class ContactAppService : ChatAppService, IContactAppService
{
    private readonly IChatUserLookupService _chatUserLookupService;
    private readonly IConversationRepository _conversationRepository;
    private readonly IConversationMemberRepository _conversationMemberRepository;
    private readonly IPermissionFinder _permissionFinder;

    public ContactAppService(
        IChatUserLookupService chatUserLookupService,
        IConversationRepository conversationRepository,
        IConversationMemberRepository conversationMemberRepository,
        IPermissionFinder permissionFinder)
    {
        _chatUserLookupService = chatUserLookupService;
        _conversationRepository = conversationRepository;
        _conversationMemberRepository = conversationMemberRepository;
        _permissionFinder = permissionFinder;
    }

    public virtual async Task<List<ChatContactDto>> GetContactsAsync(GetContactsInput input)
    {
        try
        {
            var currentUserId = CurrentUser.GetId();
            var conversations = await _conversationRepository.GetListByUserIdAsync(currentUserId, input.Filter ?? string.Empty);
            var conversationContacts = new List<ChatContactDto>();

            var conversationRows = conversations.Where(x => x?.Conversation != null).ToList();
            var allConversationIds = conversationRows.Select(x => x.Conversation!.Id).Distinct().ToList();
            IReadOnlyDictionary<Guid, ConversationMember> membersByConversation = new Dictionary<Guid, ConversationMember>();
            IReadOnlyDictionary<Guid, int> activeMemberCountsByConversation = new Dictionary<Guid, int>();
            try
            {
                if (allConversationIds.Count > 0)
                {
                    membersByConversation = await _conversationMemberRepository.GetDictionaryByConversationIdsAndUserIdAsync(allConversationIds, currentUserId);
                    var idsForGroupMemberCount = conversationRows
                        .Where(x => x.Conversation!.Type != ConversationType.User)
                        .Select(x => x.Conversation!.Id)
                        .Distinct()
                        .ToList();
                    if (idsForGroupMemberCount.Count > 0)
                    {
                        activeMemberCountsByConversation = await _conversationMemberRepository.GetActiveMemberCountsByConversationIdsAsync(idsForGroupMemberCount);
                    }
                }
            }
            catch
            {
                membersByConversation = new Dictionary<Guid, ConversationMember>();
                activeMemberCountsByConversation = new Dictionary<Guid, int>();
            }

            foreach (var x in conversations)
            {
                if (x?.Conversation == null) continue;

                var isPinned = false;
                DateTime? pinnedDate = null;
                string memberRole = null;
                var unreadMessageCount = 0;
                if (membersByConversation.TryGetValue(x.Conversation.Id, out var member))
                {
                    if (x.Conversation.Type != ConversationType.User)
                    {
                        isPinned = member.IsPinned;
                        pinnedDate = member.PinnedDate;
                        memberRole = member.Role;
                    }
                    unreadMessageCount = member.UnreadMessageCount;
                }

                var memberCount = 0;
                if (x.Conversation.Type != ConversationType.User)
                {
                    memberCount = activeMemberCountsByConversation.GetValueOrDefault(x.Conversation.Id);
                }
                
                conversationContacts.Add(new ChatContactDto
                {
                    UserId = x.TargetUser?.Id ?? Guid.Empty,
                    Name = x.TargetUser?.Name,
                    Surname = x.TargetUser?.Surname,
                    Username = x.TargetUser?.UserName,
                    LastMessage = x.Conversation.LastMessage,
                    LastMessageDate = x.Conversation.LastMessageDate,
                    UnreadMessageCount = unreadMessageCount, // Get from ConversationMember
                    Type = x.Conversation.Type,
                    ConversationName = x.Conversation.Name,
                    ConversationId = x.Conversation.Id,
                    IsPinned = isPinned,
                    PinnedDate = pinnedDate,
                    MemberCount = memberCount,
                    MemberRole = memberRole,
                    ProjectId = x.Conversation.ProjectId, // For Project conversations
                    TaskId = x.Conversation.TaskId // For Task conversations
                });
            }

            if (input.IncludeOtherContacts)
            {
                try
                {
                    var lookupUsers = await _chatUserLookupService.SearchAsync(
                        nameof(ChatUser.UserName),
                        input.Filter ?? string.Empty,
                        maxResultCount: ChatConsts.OtherContactLimitPerRequest);

                    var lookupContacts = lookupUsers?
                        .Where(x => x != null && !(conversationContacts.Any(c => c.Username == x.UserName) || x.Id == CurrentUser.Id))
                        .Select(x => new ChatContactDto
                        {
                            UserId = x.Id,
                            Name = x.Name,
                            Surname = x.Surname,
                            Username = x.UserName
                        }) ?? Enumerable.Empty<ChatContactDto>();

                    conversationContacts.AddRange(lookupContacts);
                }
                catch
                {
                    // Ignore errors when searching for other contacts
                }
            }

            // Check permissions (skip for group/project/task conversations and current user)
            try
            {
                var contactsToCheck = conversationContacts
                    .Where(x => x.UserId != Guid.Empty && x.UserId != currentUserId && x.Type == ConversationType.User)
                    .ToList();
                
                if (contactsToCheck.Any())
                {
                    var result = await _permissionFinder.IsGrantedAsync(contactsToCheck
                        .Select(x => new IsGrantedRequest
                        {
                            UserId = x.UserId,
                            PermissionNames = new[]
                            {
                                ChatPermissions.Messaging
                            }
                        })
                        .ToList());

                    foreach (var contactDto in conversationContacts)
                    {
                        if (contactDto.UserId != Guid.Empty)
                        {
                            // Current user always has permission, group conversations don't need permission check
                            if (contactDto.UserId == currentUserId || contactDto.Type != ConversationType.User)
                            {
                                contactDto.HasChatPermission = true;
                            }
                            else
                            {
                                contactDto.HasChatPermission = result?.Any(x => x.UserId == contactDto.UserId && x.Permissions?.All(p => p.Value) == true) ?? false;
                            }
                        }
                        else
                        {
                            // Group conversations without UserId always have permission
                            contactDto.HasChatPermission = true;
                        }
                    }
                }
                else
                {
                    // No direct contacts to check, set all to true (current user or group conversations)
                    foreach (var contactDto in conversationContacts)
                    {
                        contactDto.HasChatPermission = true;
                    }
                }
            }
            catch
            {
                // If permission check fails, set all to true (better UX than blocking everything)
                foreach (var contactDto in conversationContacts)
                {
                    contactDto.HasChatPermission = true;
                }
            }

            // Sort: pinned first (by pinned date descending), then by last message date descending
            var sortedContacts = conversationContacts
                .OrderByDescending(c => c.IsPinned) // Pinned first
                .ThenByDescending(c => c.IsPinned ? (c.PinnedDate ?? DateTime.MinValue) : DateTime.MinValue) // Pinned by date (newest first)
                .ThenByDescending(c => c.LastMessageDate ?? DateTime.MinValue) // Then by last message date (newest first)
                .ToList();

            // Apply pagination
            if (input.MaxResultCount > 0)
            {
                sortedContacts = sortedContacts
                    .Skip(input.SkipCount)
                    .Take(input.MaxResultCount)
                    .ToList();
            }

            return sortedContacts;
        }
        catch (Exception ex)
        {
            // Log error using Logger extension method
            Logger?.LogError(ex, "Error in GetContactsAsync");
            throw;
        }
    }

    public virtual async Task<int> GetTotalUnreadMessageCountAsync()
    {
        try
        {
            var currentUserId = CurrentUser.GetId();
            var allMembers = await _conversationMemberRepository.GetByUserIdAsync(currentUserId);
            
            var totalUnreadCount = allMembers
                .Where(m => m.IsActive)
                .Sum(m => m.UnreadMessageCount);
            
            return totalUnreadCount;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error in GetTotalUnreadMessageCountAsync");
            return 0;
        }
    }
}
