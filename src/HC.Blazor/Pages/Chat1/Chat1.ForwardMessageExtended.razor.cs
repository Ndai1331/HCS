using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HC.Chat.Conversations;
using HC.Chat.Messages;
using HC.Chat.Users;
using Microsoft.Extensions.Logging;

namespace HC.Blazor.Pages.Chat1;

/// <summary>
/// Partial class for forward message functionality
/// </summary>
public partial class Chat1
{
    // Forward message state
    protected bool ShowForwardMessageModal { get; set; }
    protected ChatMessageDto ForwardingMessage { get; set; }
    protected List<ChatContactDto> ForwardConversationList { get; set; } = new();
    protected ChatContactDto SelectedForwardConversation { get; set; }
    protected string ForwardSearchValue { get; set; } = string.Empty;
    protected string ForwardAdditionalComment { get; set; } = string.Empty;
    protected bool IsLoadingForwardConversations { get; set; }
    protected bool IsForwardingMessage { get; set; }
    
    /// <summary>
    /// Show forward message modal
    /// </summary>
    protected async Task ForwardMessageAsync(ChatMessageDto message)
    {
        try
        {
            ForwardingMessage = message;
            ForwardSearchValue = string.Empty;
            ForwardAdditionalComment = string.Empty;
            SelectedForwardConversation = null;
            IsLoadingForwardConversations = true;
            ShowForwardMessageModal = true;
            
            await InvokeAsync(StateHasChanged);
            
            // Load conversations for forwarding
            await LoadForwardConversationsAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }
    
    /// <summary>
    /// Load conversations available for forwarding
    /// </summary>
    private async Task LoadForwardConversationsAsync()
    {
        try
        {
            IsLoadingForwardConversations = true;
            await InvokeAsync(StateHasChanged);
            
            var input = new GetContactsInput
            {
                Filter = ForwardSearchValue ?? string.Empty,
                IncludeOtherContacts = false,
                MaxResultCount = 50,
                SkipCount = 0
            };
            
            var contacts = await ContactAppService.GetContactsAsync(input);
            
            // Filter out current conversation
            ForwardConversationList = contacts
                .Where(c => c.ConversationId != CurrentConversationId)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading forward conversations");
            ForwardConversationList = new List<ChatContactDto>();
        }
        finally
        {
            IsLoadingForwardConversations = false;
            await InvokeAsync(StateHasChanged);
        }
    }
    
    /// <summary>
    /// Handle search input for forward conversations
    /// </summary>
    private async Task OnForwardSearchKeyup()
    {
        await LoadForwardConversationsAsync();
    }
    
    /// <summary>
    /// Select a conversation to forward to
    /// </summary>
    private void SelectForwardConversation(ChatContactDto conversation)
    {
        if (SelectedForwardConversation?.ConversationId == conversation.ConversationId)
        {
            // Deselect if already selected
            SelectedForwardConversation = null;
        }
        else
        {
            SelectedForwardConversation = conversation;
        }
        InvokeAsync(StateHasChanged);
    }
    
    /// <summary>
    /// Execute the forward message action
    /// </summary>
    private async Task ExecuteForwardMessageAsync()
    {
        if (ForwardingMessage == null || SelectedForwardConversation?.ConversationId == null)
        {
            return;
        }
        
        try
        {
            IsForwardingMessage = true;
            await InvokeAsync(StateHasChanged);
            
            var input = new ForwardMessageInput
            {
                MessageId = ForwardingMessage.Id,
                TargetConversationId = SelectedForwardConversation.ConversationId.Value,
                AdditionalComment = string.IsNullOrWhiteSpace(ForwardAdditionalComment) ? null : ForwardAdditionalComment
            };
            
            var forwardedMessage = await ConversationAppService.ForwardMessageAsync(input);
            
            // Update LastMessage in conversation list for real-time update
            var targetConversation = ChatContactDtos.FirstOrDefault(c => 
                c.ConversationId.HasValue && c.ConversationId.Value == SelectedForwardConversation.ConversationId.Value);
            
            if (targetConversation != null)
            {
                // Update last message info
                targetConversation.LastMessage = forwardedMessage.Message;
                targetConversation.LastMessageDate = forwardedMessage.MessageDate;
                
                // Move conversation to top of list
                ChatContactDtos.Remove(targetConversation);
                ChatContactDtos.Insert(0, targetConversation);
            }
            
            // Close modal and show success
            CloseForwardMessageModal();
            
            // Optionally switch to the target conversation
            // await SetActiveAsync(SelectedForwardConversation);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsForwardingMessage = false;
            await InvokeAsync(StateHasChanged);
        }
    }
    
    /// <summary>
    /// Close the forward message modal
    /// </summary>
    private void CloseForwardMessageModal()
    {
        ShowForwardMessageModal = false;
        ForwardingMessage = null;
        SelectedForwardConversation = null;
        ForwardSearchValue = string.Empty;
        ForwardAdditionalComment = string.Empty;
        ForwardConversationList = new List<ChatContactDto>();
        InvokeAsync(StateHasChanged);
    }
}
