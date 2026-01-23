using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using HC.ProjectMembers;
using HC.Chat.Conversations;
using HC.Chat.Messages;
using HC.Chat.Users;
using HC.Shared;
using HC.Blazor.Extensions;
using Blazorise;
using Microsoft.AspNetCore.Components.Web;


namespace HC.Blazor.Pages.Chat1.InfomationConversations;

public partial class InfoBox : HCComponentBase, IAsyncDisposable
{
    [Inject]
    public IJSRuntime JsRuntime { get; set; }

    [Parameter]
    public ChatContactDto CurrentChatContact { get; set; }

    [Parameter]
    public Func<Task> ShowInfoBoxAsync { get; set; } = null!;

    [Parameter]
    public Func<ConversationMemberDto, Task> SendMessageToMemberAsync { get; set; } = null!;
    [Parameter]
    public Func<Guid, Task> DownloadFileAsync { get; set; } = null!;
    [Parameter]
    public Func<AddMemberInput, Task> AddMembersAsync { get; set; } = null!;
    [Parameter]
    public Func<RemoveMemberInput, Task> RemoveMemberAsync { get; set; } = null!;
    [Parameter]
    public Func<RemoveMemberInput, Task> LeaveConversationAsync { get; set; } = null!;

    public bool AccordionChatInfoVisible { get; set; } = false;
    public bool AccordionChatMembersVisible { get; set; } = false;
    public bool AccordionMediaFilesVisible { get; set; } = false;

    private int MaxResultCount { get; set; } = 10;
    private int SkipFindMessageCount { get; set; } = 0;
    private bool ShowLoadMoreFoundMessages { get; set; } = false;
    private List<ChatMessageDto> FoundMessages { get; set; } = new();

    [Parameter]
    public Dictionary<ChatContactDto, ElementReference> CanvasElementReferences { get; set; } = null!;

    [Parameter]
    public Func<ChatContactDto, string> GetName { get; set; } = null!;
    [Parameter]
    public Func<ChatContactDto, string> GetContactDisplayName { get; set; } = null!;
    public IReadOnlyList<LookupDto<Guid>> IdentityUsersCollection { get; set; } = null!;

    [Parameter]
    public List<LookupDto<Guid>> SelectedDirectUser { get; set; } = null!;
    public List<LookupDto<Guid>> SelectedMembersToAddConversation { get; set; } = new();
    [Parameter]
    public List<LookupDto<Guid>> SelectedProject { get; set; } = null!;
    public List<LookupDto<Guid>> SelectedTask { get; set; } = null!;
    [Inject]
    public IConversationAppService ConversationService { get; set; } = default!;

    private List<ConversationMemberDto> Members { get; set; } = new();

    [Parameter]
    public Guid? CurrentUserId { get; set; }
    private bool IsLoadingMembers { get; set; } = false;
    private ChatContactDto? _previousChatContact;

    private Modal PinnedMessagesModal { get; set; } = new();
    private Modal AddMembersModal { get; set; } = new();

    AddMemberInput AddMembersInput { get; set; } = new();

    private string SelectedTabMediaFiles { get; set; } = "images";

    private bool IsCurrentUserAdmin { get; set; } = false;
    private bool ShowFindMessage { get; set; } = false;
    private string SearchMessageValue { get; set; } = string.Empty;
    private List<ChatMessageDto> PinnedMessages {get;set;} = new();

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        // Chỉ load members khi CurrentChatContact thay đổi và không phải chat cá nhân
        if (CurrentChatContact != null &&
            CurrentChatContact.Type != ConversationType.User &&
            (_previousChatContact == null || _previousChatContact.ConversationId != CurrentChatContact.ConversationId))
            await LoadConversationMembersAsync();
            CheckIsCurrentUserAdminAsync();
            await CloseFindMessageAsync();
            _previousChatContact = CurrentChatContact;

    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        // Tạo avatar khi accordion members được mở và đã có members sẵn
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

    private async Task LoadConversationMembersAsync()
    {
        IsLoadingMembers = true;
        if (CurrentChatContact is not null && CurrentChatContact.Type != ConversationType.User)
        {
            Members = await ConversationService.GetMembersAsync(CurrentChatContact.ConversationId.Value);
        }
        else
        {
            Members = new List<ConversationMemberDto>();
        }

        IsLoadingMembers = false;
        await InvokeAsync(StateHasChanged);
    }
    private async Task FindMessageAsync()
    {
        ShowFindMessage = true;
        FoundMessages.Clear();
        SkipFindMessageCount = 0;
        ShowLoadMoreFoundMessages = false;
        await InvokeAsync(StateHasChanged);
    }

    private async Task ShowPinnedMessagesAsync()
    {
        await BlockUiService.Block(selectors: "#chat_wrapper", busy: true  );
        PinnedMessages = await ConversationService.GetPinnedMessagesAsync(CurrentChatContact!.ConversationId!.Value);
        await PinnedMessagesModal.Show();
        await BlockUiService.UnBlock();
        await InvokeAsync(StateHasChanged);
    }

    private async Task ClosePinnedMessagesModalAsync(){
        await PinnedMessagesModal.Hide();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnSelectedTabMediaFilesChanged(string name)
    {
        SelectedTabMediaFiles = name;
        await InvokeAsync(StateHasChanged);
    }

    private async Task ShowModalAddMembersAsync(Guid conversationId)
    {
        AddMembersInput.ConversationId = conversationId;
        SelectedMembersToAddConversation = new();
        await AddMembersModal.Show();
    }

    private async Task CloseAddMembersModalAsync()
    {
        await AddMembersModal.Hide();
    }

    private void CheckIsCurrentUserAdminAsync()
    {
        IsCurrentUserAdmin = Members.Any(m => m.UserId == CurrentUserId && m.Role == "ADMIN");
    }

    private async Task<List<LookupDto<Guid>>> GetIdentityUserCollectionLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var allUsers = (await ProjectMembersAppService.GetIdentityUserLookupAsync(new LookupRequestDto { Filter = filter })).Items;
        var currentUserId = CurrentUser.Id ?? Guid.Empty;
        // Filter out current user
        IdentityUsersCollection = allUsers.Where(u => u.Id != currentUserId).ToList();
        return IdentityUsersCollection.ToList();
    }

    private async Task RemoveMemberFromInfoBoxAsync(RemoveMemberInput input)
    {
        if(RemoveMemberAsync != null)
        {
            await RemoveMemberAsync(input);
            await LoadConversationMembersAsync();
        }
    }

    private async Task LeaveConversationFromInfoBoxAsync(RemoveMemberInput input)
    {
        if(LeaveConversationAsync != null)
        {
            await LeaveConversationAsync(input);
            await LoadConversationMembersAsync();
        }
    }

    private async Task AddMembersToInfoBoxAsync()
    {
        if(AddMembersAsync != null)
        {
            await CloseAddMembersModalAsync();
            await AddMembersAsync(new AddMemberInput { 
                ConversationId = CurrentChatContact.ConversationId!.Value, 
                UserIds = SelectedMembersToAddConversation.Select(x => x.Id).ToList() ?? new List<Guid>() });
            await LoadConversationMembersAsync();
        }
    }

    private async Task OnSearchMessageKeyupAsync(KeyboardEventArgs e)
    {
        if(e.Key == "Enter")
        {
            FoundMessages.Clear();
            ShowLoadMoreFoundMessages = false;
            SkipFindMessageCount = 0;

            await BlockUiService.Block(selectors: "#found_messages", busy: true  );

            var listMessages = await ConversationService.FindMessagesInConversationAsync(new FindMessageInConversationInput 
            { ConversationId = CurrentChatContact!.ConversationId!.Value,
             MessageText = SearchMessageValue,
             MaxResultCount = MaxResultCount,
             SkipCount = SkipFindMessageCount });

            if(listMessages.Count > 0)
            {
                FoundMessages.AddRange(listMessages);
                if(listMessages.Count < MaxResultCount)
                {
                    ShowLoadMoreFoundMessages = false;
                }
                else
                {
                    ShowLoadMoreFoundMessages = true;
                }
            }
            else
            {
                ShowLoadMoreFoundMessages = false;
            }

            await InvokeAsync(StateHasChanged);
            await BlockUiService.UnBlock();
        }
    }

    private async Task LoadMoreFoundMessagesAsync()
    {
        SkipFindMessageCount += MaxResultCount;

        var listMessages = await ConversationService.FindMessagesInConversationAsync(new FindMessageInConversationInput 
            { ConversationId = CurrentChatContact!.ConversationId!.Value,
             MessageText = SearchMessageValue,
             MaxResultCount = MaxResultCount,
             SkipCount = SkipFindMessageCount }) ;

        if(listMessages.Count > 0)
        {
            FoundMessages.AddRange(listMessages);
            if(listMessages.Count < MaxResultCount)
            {
                ShowLoadMoreFoundMessages = false;
            }
            else
            {
                ShowLoadMoreFoundMessages = true;
            }
        }
        else
        {
            ShowLoadMoreFoundMessages = false;
        }
        await InvokeAsync(StateHasChanged);
    }



    private async Task CloseFindMessageAsync()
    {
        ShowFindMessage = false;
        SearchMessageValue=string.Empty;
        FoundMessages.Clear();
        ShowLoadMoreFoundMessages = false;
        SkipFindMessageCount = 0;
        await InvokeAsync(StateHasChanged);
    }
}