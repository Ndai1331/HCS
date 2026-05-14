using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using HC.Chat.Conversations;
using HC.Chat.Messages;
using HC.ProjectMembers;
using HC.Shared;
using HC.Blazor.Extensions;
using Blazorise;
using HC.Chat.Users;
using HC.DocumentFiles;
using Microsoft.Extensions.Logging;
using Volo.Abp.AspNetCore.Components.Messages;


namespace HC.Blazor.Pages.Chat1.InfomationConversations;

public partial class InfoBox : HCComponentBase, IAsyncDisposable
{
    private const string RoleAdmin = "ADMIN";
    private const string RoleMember = "MEMBER";

    [Inject]
    public IJSRuntime JsRuntime { get; set; }

    [Inject]
    public IContactAppService ContactAppService { get; set; } = default!;

    [Parameter]
    public ChatContactDto CurrentChatContact { get; set; }

    [Parameter]
    public Func<Task> ShowInfoBoxAsync { get; set; } = null!;

    [Parameter]
    public Func<Task> HideInfoBoxAsync { get; set; } = null!;

    [Parameter]
    public bool IsMobileMode { get; set; }

    [Parameter]
    public Func<string, string> GetImageUrl { get; set; } = null!;
    [Parameter]
    public Func<ConversationMemberDto, Task> SendMessageToMemberAsync { get; set; } = null!;
    [Parameter]
    public Func<Guid, Task> DownloadFileAsync { get; set; } = null!;
    [Parameter]
    public Func<Guid, Task> JumpToMessageAsync { get; set; } = null!;
    [Parameter]
    public Func<AddMemberInput, Task> AddMembersAsync { get; set; } = null!;
    [Parameter]
    public Func<RemoveMemberInput, Task> RemoveMemberAsync { get; set; } = null!;
    [Parameter]
    public Func<RemoveMemberInput, Task> LeaveConversationAsync { get; set; } = null!;
    [Parameter]
    public EventCallback<MessageFileDto> OnOpenImageViewer { get; set; }

    public bool AccordionChatInfoVisible { get; set; }
    public bool AccordionChatMembersVisible { get; set; }
    public bool AccordionMediaFilesVisible { get; set; }

    private int MaxResultCount { get; } = 10;
    private int SkipFindMessageCount { get; set; }
    private bool ShowLoadMoreFoundMessages { get; set; }

    private bool ShowLoadMoreImages { get; set; }
    private bool ShowLoadMoreFiles { get; set; }

    private int SkipImagesCount { get; set; }
    private int SkipFilesCount { get; set; }

    private List<MessageFileDto> FoundMedias { get; set; } = new();
    private List<MessageFileDto> FoundFiles { get; set; } = new();
    private bool IsLoadingMediaAndFiles { get; set; }

    private Dictionary<string, bool> ShowDivs = new()
    {
        {"Infobox", true},
        {"FindMessage", false},
        {"MediaAndFile", false},
    };

    // Method để set active div
    private async Task SetActiveDivAsync(string key)
    {
        foreach (var k in ShowDivs.Keys.ToList())
        {
            ShowDivs[k] = k == key;
        }
        await InvokeAsync(StateHasChanged);
    }


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
    private bool IsLoadingMembers { get; set; }
    /// <summary>Last conversation id for which members were loaded (stable key; avoids refetch on parent re-render with new DTO instance).</summary>
    private Guid? _lastLoadedMembersConversationId;
    private string? _memberListSignatureForAvatars;

    [Parameter]
    public Guid? CurrentUserId { get; set; }

    private Modal PinnedMessagesModal { get; set; } = new();
    private Modal AddMembersModal { get; set; } = new();
    private AddMemberInput AddMembersInput { get; } = new();

    private string SelectedTabMediaFiles { get; set; } = "images";
    private bool IsCurrentUserAdmin { get; set; }
    private bool ShowFindMessage { get; set; }

    private void ResetAccordionToDefault()
    {
        AccordionChatInfoVisible = false;
        AccordionChatMembersVisible = false;
        AccordionMediaFilesVisible = false;
    }
    private string SearchMessageValue { get; set; } = "";
    private List<ChatMessageDto> PinnedMessages { get; set; } = new();

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        if (CurrentChatContact is null || CurrentChatContact.Type == ConversationType.User)
        {
            return;
        }

        if (!CurrentChatContact.ConversationId.HasValue)
        {
            Members = new();
            IsCurrentUserAdmin = false;
            _lastLoadedMembersConversationId = null;
            _memberListSignatureForAvatars = null;
            return;
        }

        var conversationId = CurrentChatContact.ConversationId.Value;
        if (_lastLoadedMembersConversationId == conversationId)
        {
            return;
        }

        var hadDifferentConversation = _lastLoadedMembersConversationId.HasValue
            && _lastLoadedMembersConversationId.Value != conversationId;
        if (hadDifferentConversation)
        {
            ResetAccordionToDefault();
            await CloseFindMessageAsync();
        }

        await LoadConversationMembersAsync();
        CheckIsCurrentUserAdminAsync();
        _lastLoadedMembersConversationId = conversationId;
        _memberListSignatureForAvatars = null;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if(firstRender)
        {
            await SetActiveDivAsync("Infobox");
        }
        if (!firstRender && AccordionChatMembersVisible && Members.Any() && !IsLoadingMembers)
        {
            var signature = string.Join(',', Members.Select(m => m.UserId));
            if (!string.Equals(_memberListSignatureForAvatars, signature, StringComparison.Ordinal))
            {
                _memberListSignatureForAvatars = signature;
                await CreateMemberAvatarsAsync();
            }
        }
    }

    private async Task CreateMemberAvatarsAsync()
    {
        await Task.Delay(300);

        foreach (var member in Members)
        {
            try
            {
                var canvasId = $"member-avatar-{member.UserId}";
                var displayName = $"{member.UserInfo.Name} {member.UserInfo.Surname}".Trim();
                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = member.UserInfo.Username ?? "";

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
        Members = (CurrentChatContact?.Type != ConversationType.User && CurrentChatContact?.ConversationId.HasValue == true)
            ? await ConversationService.GetMembersAsync(CurrentChatContact.ConversationId.Value)
            : new List<ConversationMemberDto>();

        IsLoadingMembers = false;
        await InvokeAsync(StateHasChanged);
    }
    private async Task FindMessageAsync()
    {
        await SetActiveDivAsync("FindMessage");
        FoundMessages.Clear();
        SkipFindMessageCount = 0;
        ShowLoadMoreFoundMessages = false;
    }

    private async Task ShowPinnedMessagesAsync()
    {
        await BlockUiService.Block(selectors: "#chat_wrapper", busy: true);
        PinnedMessages = await ConversationService.GetPinnedMessagesAsync(CurrentChatContact!.ConversationId!.Value);
        await PinnedMessagesModal.Show();
        await BlockUiService.UnBlock();
        await InvokeAsync(StateHasChanged);
    }

    private async Task ClosePinnedMessagesModalAsync() => await CloseModalAsync(PinnedMessagesModal);
    private async Task OnSelectedTabMediaFilesChanged(string name)
    {
        SelectedTabMediaFiles = name;
        
        // Auto load data when tab changes
        if (name == "images")
        {
            SkipImagesCount = 0;
            FoundMedias.Clear();
            ShowLoadMoreImages = false;
        }
        else if (name == "files")
        {
            SkipFilesCount = 0;
            FoundFiles.Clear();
            ShowLoadMoreFiles = false;
        }
        
        var fileType = name == "images" ? FileMediaType.Image : FileMediaType.File;
        await InvokeAsync(StateHasChanged);
        await SearchMediaAndFileAsync(fileType);
    }

    private async Task ShowModalAddMembersAsync(Guid conversationId)
    {
        AddMembersInput.ConversationId = conversationId;
        SelectedMembersToAddConversation = new();
        await AddMembersModal.Show();
    }

    private async Task CloseAddMembersModalAsync() => await CloseModalAsync(AddMembersModal);

    private void CheckIsCurrentUserAdminAsync()
    {
        IsCurrentUserAdmin = Members.Any(m => m.UserId == CurrentUserId && m.Role == "ADMIN");
    }

    private async Task<List<LookupDto<Guid>>> GetIdentityUserCollectionLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        IdentityUsersCollection = (await ContactAppService.GetUserLookupAsync(new LookupRequestDto
        {
            Filter = filter,
            MaxResultCount = 20,
            SkipCount = 0
        })).Items;

        return IdentityUsersCollection.ToList();
    }

    private async Task RemoveMemberFromInfoBoxAsync(RemoveMemberInput input) =>
        await ExecuteMemberActionAsync(() => RemoveMemberAsync?.Invoke(input));

    private async Task LeaveConversationFromInfoBoxAsync(RemoveMemberInput input)
    {
        if (LeaveConversationAsync != null)
        {
            await LeaveConversationAsync.Invoke(input);
        }
    }

    private async Task SetMemberRoleFromInfoBoxAsync(Guid userId, string role)
    {
        if (CurrentChatContact?.ConversationId == null) return;
        try
        {
            await BlockUiService.Block(selectors: "#chat_wrapper", busy: true);
            try
            {
                await ConversationService.SetMemberRoleAsync(new SetMemberRoleInput
                {
                    ConversationId = CurrentChatContact.ConversationId.Value,
                    UserId = userId,
                    Role = role
                });
                await LoadConversationMembersAsync();
                CheckIsCurrentUserAdminAsync();
            }
            finally
            {
                await BlockUiService.UnBlock();
            }
        }
        catch (Exception ex)
        {
            try { await BlockUiService.UnBlock(); }
            catch (Exception unblockEx) { Logger.LogDebug(unblockEx, "BlockUiService.UnBlock failed (ignored)."); }
            await UiMessageService.Error(ex.Message,
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
        }
    }

    private bool IsOnlyAdmin => Members.Count(m => m.Role == "ADMIN") <= 1 
                                && Members.Any(m => m.UserId == CurrentUserId && m.Role == "ADMIN");

    private async Task AddMembersToInfoBoxAsync()
    {
        if (AddMembersAsync != null && SelectedMembersToAddConversation.Any())
        {
            await CloseAddMembersModalAsync();
            await AddMembersAsync(new AddMemberInput
            {
                ConversationId = CurrentChatContact.ConversationId!.Value,
                UserIds = SelectedMembersToAddConversation.Select(x => x.Id).ToList()
            });
            await LoadConversationMembersAsync();
        }
    }

    private async Task ExecuteMemberActionAsync(Func<Task> action)
    {
        if (action != null)
        {
            await action();
            await LoadConversationMembersAsync();
        }
    }

    private async Task OnSearchMessageKeyupAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(SearchMessageValue))
        {
            FoundMessages.Clear();
            ShowLoadMoreFoundMessages = false;
            SkipFindMessageCount = 0;
            await InvokeAsync(StateHasChanged);
            await SearchMessagesAsync();
        }
        else if (e.Key == "Escape")
        {
            FoundMessages.Clear();
            ShowLoadMoreFoundMessages = false;
            SkipFindMessageCount = 0;
        }
    }



    private async Task LoadMoreFoundMessagesAsync()
    {
        SkipFindMessageCount += MaxResultCount;
        await SearchMessagesAsync();
    }

    private async Task LoadMoreImagesAsync()
    {
        SkipImagesCount += MaxResultCount;
        await SearchMediaAndFileAsync(FileMediaType.Image);
    }

    private async Task LoadMoreFilesAsync()
    {
        SkipFilesCount += MaxResultCount;
        await SearchMediaAndFileAsync(FileMediaType.File);
    }

    private async Task SearchMessagesAsync()
    {
        await BlockUiService.Block(selectors: "#found_messages", busy: true);

        var listMessages = await ConversationService.FindMessagesInConversationAsync(new FindMessageInConversationInput
        {
            ConversationId = CurrentChatContact!.ConversationId!.Value,
            MessageText = SearchMessageValue,
            MaxResultCount = MaxResultCount,
            SkipCount = SkipFindMessageCount
        });

        // Chỉ AddRange nếu đang Load More (SkipFindMessageCount > 0)
        if (SkipFindMessageCount > 0)
        {
            FoundMessages.AddRange(listMessages);
        }
        else
        {
            // Search mới: Clear trước rồi mới Add
            FoundMessages.Clear();
            FoundMessages.AddRange(listMessages);
        }

        ShowLoadMoreFoundMessages = listMessages.Count >= MaxResultCount;

        await InvokeAsync(StateHasChanged);
        await BlockUiService.UnBlock();
    }

    private async Task OpenFoundMessageAsync(Guid messageId)
    {
        if (JumpToMessageAsync != null)
        {
            await JumpToMessageAsync.Invoke(messageId);
        }
    }

    private async Task SearchMediaAndFileAsync(FileMediaType fileType)
    {
        await BlockUiService.Block(selectors: "#found_media_and_files", busy: true);
        var mediaAndFiles = await ConversationService.FindMediaAndFileInConversationAsync(new FindMediaAndFileInConversationInput
        {
            ConversationId = CurrentChatContact!.ConversationId!.Value,
            FileName = "", // Load all files/images without filtering by name
            FileType = fileType,
            MaxResultCount = MaxResultCount,
            SkipCount = fileType == FileMediaType.Image ? SkipImagesCount : SkipFilesCount
        });

        switch (fileType)
        {
            case FileMediaType.Image:
                if (SkipImagesCount > 0)
                {
                    FoundMedias.AddRange(mediaAndFiles);
                }
                else
                {
                    FoundMedias.Clear();
                    FoundMedias.AddRange(mediaAndFiles);
                }
                ShowLoadMoreImages = mediaAndFiles.Count >= MaxResultCount;
                break;
            case FileMediaType.File:
                if (SkipFilesCount > 0)
                {
                    FoundFiles.AddRange(mediaAndFiles);
                }
                else
                {
                    FoundFiles.Clear();
                    FoundFiles.AddRange(mediaAndFiles);
                }
                ShowLoadMoreFiles = mediaAndFiles.Count >= MaxResultCount;
                break;
        }

        await InvokeAsync(StateHasChanged);
        await BlockUiService.UnBlock();
    }   


    private async Task CloseFindMessageAsync()
    {
        await SetActiveDivAsync("Infobox");
        SearchMessageValue = "";
        FoundMessages.Clear();
        ShowLoadMoreFoundMessages = false;
        SkipFindMessageCount = 0;
    }

    private async Task CloseMediaAndFileAsync()
    {
        await SetActiveDivAsync("Infobox");
        FoundMedias.Clear();
        FoundFiles.Clear();
        ShowLoadMoreImages = false;
        ShowLoadMoreFiles = false;
        SkipImagesCount = 0;
        SkipFilesCount = 0;
    }

    // Helper methods
    private async Task CloseModalAsync(Modal modal)
    {
        await modal.Hide();
        await InvokeAsync(StateHasChanged);
    }

    private async Task SetPropertyAsync(Action action)
    {
        action();
        await InvokeAsync(StateHasChanged);
    }


    // Helper method to get file URL from API
    private string GetFileUrl(DocumentFileDto file)
    {
        // Adjust this based on your API endpoint for file preview
        return $"/api/files/{file.Id}/download";
    }

    private async Task ShowImagesAsync()
    {
        await SetActiveDivAsync("MediaAndFile");
        SelectedTabMediaFiles = "images";
        await SearchMediaAndFileAsync(FileMediaType.Image);
    }

    private async Task ShowFilesAsync()
    {
        await SetActiveDivAsync("MediaAndFile");
        SelectedTabMediaFiles = "files";
        await SearchMediaAndFileAsync(FileMediaType.File);
    }

    
    private async Task OnOpenImageViewerAsync(MessageFileDto file)
    {
        await OnOpenImageViewer.InvokeAsync(file);
    }

}
