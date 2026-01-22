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


    
    [Parameter]
    public ChatContactDto CurrentChatContact { get; set; }

    [Parameter]
    public Func<Task> ShowPinnedMessagesAsync { get; set; } = null!;

    [Parameter]
    public Func<Task> ShowInfoBoxAsync { get; set; } = null!;


    public bool AccordionChatInfoVisible { get; set; } = false;
    public bool AccordionChatMembersVisible { get; set; } = false;
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

}