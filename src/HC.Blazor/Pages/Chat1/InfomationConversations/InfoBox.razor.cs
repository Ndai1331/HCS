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

    private string SearchMediaAndFileValue { get; set; } = "";
    private List<ChatMessageDto> FoundMediaAndFiles { get; set; } = new();
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
    private ChatContactDto? _previousChatContact;

    [Parameter]
    public Guid? CurrentUserId { get; set; }

    private Modal PinnedMessagesModal { get; set; } = new();
    private Modal AddMembersModal { get; set; } = new();
    private AddMemberInput AddMembersInput { get; } = new();

    private string SelectedTabMediaFiles { get; set; } = "images";
    private bool IsCurrentUserAdmin { get; set; }
    private bool ShowFindMessage { get; set; }
    private string SearchMessageValue { get; set; } = "";
    private List<ChatMessageDto> PinnedMessages { get; set; } = new();

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        if (CurrentChatContact?.Type != ConversationType.User &&
            (_previousChatContact?.ConversationId != CurrentChatContact.ConversationId))
        {
            await LoadConversationMembersAsync();
            CheckIsCurrentUserAdminAsync();
            await CloseFindMessageAsync();
            _previousChatContact = CurrentChatContact;
        }
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
            await CreateMemberAvatarsAsync();
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
        Members = CurrentChatContact?.Type != ConversationType.User
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
    private async Task OnSelectedTabMediaFilesChanged(string name) => await SetPropertyAsync(() => SelectedTabMediaFiles = name);

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
        var allUsers = (await ProjectMembersAppService.GetIdentityUserLookupAsync(new LookupRequestDto { Filter = filter })).Items;
        var currentUserId = CurrentUser.Id ?? Guid.Empty;
        var filteredUsers = allUsers.Where(u => u.Id != currentUserId).ToList();
        IdentityUsersCollection = filteredUsers;
        return filteredUsers;
    }

    private async Task RemoveMemberFromInfoBoxAsync(RemoveMemberInput input) =>
        await ExecuteMemberActionAsync(() => RemoveMemberAsync?.Invoke(input));

    private async Task LeaveConversationFromInfoBoxAsync(RemoveMemberInput input) =>
        await ExecuteMemberActionAsync(() => LeaveConversationAsync?.Invoke(input));

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

    // Handle input clear (when user clicks the X button in search input)
    private async Task OnSearchInputChangeAsync()
    {
        // Nếu input trống (user click X để clear), reset danh sách tìm kiếm
        if (string.IsNullOrWhiteSpace(SearchMessageValue))
        {
            FoundMessages.Clear();
            SkipFindMessageCount = 0;
            ShowLoadMoreFoundMessages = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task LoadMoreFoundMessagesAsync()
    {
        SkipFindMessageCount += MaxResultCount;
        await SearchMessagesAsync();
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
        SearchMediaAndFileValue = "";
        FoundMediaAndFiles.Clear();
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

    // Helper method to check if file is an image
    private bool IsImageFile(string fileName)
    {
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".ico" };
        var extension = System.IO.Path.GetExtension(fileName).ToLower();
        return imageExtensions.Contains(extension);
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
    }

    private async Task ShowFilesAsync()
    {
        await SetActiveDivAsync("MediaAndFile");
        SelectedTabMediaFiles = "files";
    }

    private async Task LoadMoreImagesAsync()
    {
        SkipImagesCount += MaxResultCount;
        await SearchMediaAndFilesAsync();
    }

    private async Task LoadMoreFilesAsync()
    {
        SkipFilesCount += MaxResultCount;
        await SearchMediaAndFilesAsync();
    }

    // Find Media and File functionality
    private async Task FindMediaAndFileAsync()
    {
        await SetActiveDivAsync("MediaAndFile");
        SearchMediaAndFileValue = "";
        FoundMediaAndFiles.Clear();
        SkipImagesCount = 0;
        SkipFilesCount = 0;
        ShowLoadMoreImages = false;
        ShowLoadMoreFiles = false;
        SelectedTabMediaFiles = "images";
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnSearchMediaAndFileKeyupAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(SearchMediaAndFileValue))
        {
            FoundMediaAndFiles.Clear();
            ShowLoadMoreImages = false;
            ShowLoadMoreFiles = false;
            SkipImagesCount = 0;
            SkipFilesCount = 0;
            await InvokeAsync(StateHasChanged);
            await SearchMediaAndFilesAsync();
        }
        else if (e.Key == "Escape")
        {
            await CloseMediaAndFileAsync();
        }
    }

    // Handle input clear for media and file search
    private async Task OnSearchMediaAndFileInputChangeAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchMediaAndFileValue))
        {
            FoundMediaAndFiles.Clear();
            SkipImagesCount = 0;
            SkipFilesCount = 0;
            ShowLoadMoreImages = false;
            ShowLoadMoreFiles = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task SearchMediaAndFilesAsync()
    {
        await BlockUiService.Block(selectors: "#found_media_and_files", busy: true);
        IsLoadingMediaAndFiles = true;

        try
        {
            // For now, we'll filter the found messages by files that match the search criteria
            // In a real scenario, you might want to implement a dedicated API endpoint
            var skipCount = SelectedTabMediaFiles == "images" ? SkipImagesCount : SkipFilesCount;
            var allMessages = await ConversationService.FindMessagesInConversationAsync(new FindMessageInConversationInput
            {
                ConversationId = CurrentChatContact!.ConversationId!.Value,
                MessageText = SearchMediaAndFileValue,
                MaxResultCount = MaxResultCount * 5,  // Get more to filter
                SkipCount = 0
            });

            // Filter messages with files
            var filteredMessages = allMessages
                .Where(m => m.Files != null && m.Files.Any())
                .ToList();

            // Chỉ AddRange nếu đang Load More
            if ((SelectedTabMediaFiles == "images" && SkipImagesCount > 0) || 
                (SelectedTabMediaFiles == "files" && SkipFilesCount > 0))
            {
                FoundMediaAndFiles.AddRange(filteredMessages.Skip(skipCount).Take(MaxResultCount).ToList());
            }
            else
            {
                FoundMediaAndFiles.Clear();
                FoundMediaAndFiles.AddRange(filteredMessages.Skip(skipCount).Take(MaxResultCount).ToList());
            }

            // Set load more flag dựa vào tab hiện tại
            if (SelectedTabMediaFiles == "images")
            {
                ShowLoadMoreImages = SkipImagesCount + MaxResultCount < filteredMessages.Count;
            }
            else
            {
                ShowLoadMoreFiles = SkipFilesCount + MaxResultCount < filteredMessages.Count;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error searching media and files: {ex.Message}");
        }

        IsLoadingMediaAndFiles = false;
        await InvokeAsync(StateHasChanged);
        await BlockUiService.UnBlock();
    }

}