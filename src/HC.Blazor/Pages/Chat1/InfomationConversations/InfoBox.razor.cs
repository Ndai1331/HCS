using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HC.Blazor;
using HC.Blazor.Components.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Volo.Abp.Localization;
using HC.Chat.Authorization;
using HC.Chat.Conversations;
using HC.Chat.Messages;
using HC.Chat.Settings;
using HC.Chat.Users;
using HC.Projects;
using HC.ProjectTasks;
using HC.ProjectMembers;
using HC.ProjectTaskAssignments;
using HC.Shared;
using Microsoft.Extensions.Caching.Memory;
using Volo.Abp.Application.Dtos;
using HC.Blazor.Extensions;
using Microsoft.Extensions.Logging;


namespace HC.Blazor.Pages.Chat1.InfomationConversations;

public partial class InfoBox : HCComponentBase, IAsyncDisposable
{
    [Inject]
    public IJSRuntime JsRuntime { get; set; }

    
    [Parameter]
    public ChatContactDto CurrentChatContact { get; set; }

    [Parameter]
    public Func<Task> ShowPinnedMessagesAsync { get; set; } = null!;

    [Parameter]
    public Func<Task> ShowInfoBoxAsync { get; set; } = null!;


    public bool AccordionChatInfoVisible { get; set; } = false;
    private bool _accordionChatMembersVisible = false;
    public bool AccordionChatMembersVisible
    {
        get => _accordionChatMembersVisible;
        set
        {
            if (_accordionChatMembersVisible != value)
            {
                _accordionChatMembersVisible = value;
                if (value && CurrentChatContact?.Type != ConversationType.User && Members.Count == 0)
                {
                    _ = LoadMembersAsync();
                }
                else if (value && CurrentChatContact?.Type != ConversationType.User && Members.Any())
                {
                    _ = CreateMemberAvatarsAsync();
                }
            }
        }
    }
    public bool AccordionMediaFilesVisible { get; set; } = false;

    [Parameter]
    public Dictionary<ChatContactDto, ElementReference> CanvasElementReferences { get; set; } = null!;

    [Parameter]
    public Func<ChatContactDto, string> GetName { get; set; } = null!;
    [Parameter]
    public Func<ChatContactDto, string> GetContactDisplayName { get; set; } = null!;
    [Parameter]
    public IReadOnlyList<LookupDto<Guid>> IdentityUsersCollection { get; set; } = null!;
    [Parameter]
    public List<LookupDto<Guid>> SelectedDirectUser { get; set; } = null!;
    public List<LookupDto<Guid>> SelectedMembers { get; set; } = null!;
    [Parameter]
    public List<LookupDto<Guid>> SelectedProject { get; set; } = null!;
    public List<LookupDto<Guid>> SelectedTask { get; set; } = null!;
    [Inject]
    public IConversationAppService ConversationService { get; set; } = default!;

    private List<ConversationMemberDto> Members { get; set; } = new();


    private bool IsLoadingMembers { get; set; } = false;

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        if (CurrentChatContact?.Type != ConversationType.User && AccordionChatMembersVisible)
        {
            await LoadMembersAsync();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        // Tạo avatar khi accordion members được mở và chưa có avatar
        if (!firstRender && AccordionChatMembersVisible && Members.Any() && !IsLoadingMembers)
        {
            await CreateMemberAvatarsAsync();
        }
    }

    private async Task CreateMemberAvatarsAsync()
    {
        await Task.Delay(300); // Đợi DOM ổn định

        foreach (var member in Members)
        {
            try
            {
                var canvasId = $"member-avatar-{member.UserId}";
                var displayName = !string.IsNullOrEmpty(member.UserInfo.Name) || !string.IsNullOrEmpty(member.UserInfo.Surname)
                        ? $"{member.UserInfo.Name} {member.UserInfo.Surname}".Trim()
                        : member.UserInfo.Username ?? "";

                await JsRuntime.SafeInvokeVoidAsync("VoloChatAvatarManager.createCanvasForUserById", canvasId, member.UserInfo.Username, displayName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating avatar for member {member.UserId}: {ex.Message}");
            }
        }
    }

    private async Task LoadMembersAsync()
    {
        IsLoadingMembers = true;
        if (CurrentChatContact is not null && CurrentChatContact.Type != ConversationType.User)
        {
            var data = await ConversationService.GetMembersAsync(CurrentChatContact.ConversationId.Value);
            Members = data;

            // Đợi DOM render xong trước khi tạo avatar
            await Task.Delay(200);

            foreach (var member in Members)
            {
                try
                {
                    var canvasId = $"member-avatar-{member.UserId}";
                    var displayName = !string.IsNullOrEmpty(member.UserInfo.Name) || !string.IsNullOrEmpty(member.UserInfo.Surname)
                            ? $"{member.UserInfo.Name} {member.UserInfo.Surname}".Trim()
                            : member.UserInfo.Username ?? "";

                    await JsRuntime.SafeInvokeVoidAsync("VoloChatAvatarManager.createCanvasForUserById", canvasId, member.UserInfo.Username, displayName);
                }
                catch (Exception ex)
                {
                    // Log lỗi nhưng không làm dừng quá trình
                    Console.WriteLine($"Error creating avatar for member {member.UserId}: {ex.Message}");
                }
            }
        }
        else
        {
            Members = new List<ConversationMemberDto>();
        }

        IsLoadingMembers = false;
        await InvokeAsync(StateHasChanged);
    }

    private async Task RemoveMemberAsync(ConversationMemberDto member)
    {
        await ConversationService.RemoveMemberAsync(new RemoveMemberInput { ConversationId = CurrentChatContact.ConversationId.Value, UserId = member.UserId });
        await LoadMembersAsync();
        await InvokeAsync(StateHasChanged);
    }
     
    private async Task LeaveConversationAsync(ConversationMemberDto member)
    {
        // await ConversationAppService.LeaveConversationAsync(contact.ConversationId.Value);
        // await GetContactsAsync();
        await InvokeAsync(StateHasChanged);
    }

}