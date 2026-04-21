using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Blazorise;
using Blazorise.DataGrid;
using Blazorise.RichTextEdit;
using Volo.Abp.BlazoriseUI.Components;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Components.Web.Theming.PageToolbars;
using HC.Documents;
using HC.DocumentFiles;
using HC.DocumentAssignments;
using HC.DocumentWorkflowInstances;
using HC.DocumentWorkflowInstanceLogss;
using HC.DocumentWorkflowInstanceFiles;
using HC.DocumentHistories;
using HC.MasterDatas;
using HC.Permissions;
using HC.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Volo.Abp.AspNetCore.Components.Messages;
using Volo.Abp.BlobStoring;
using HC.SignatureSettings;
using HC.UserSignatures;

namespace HC.Blazor.Pages.Documents;

public partial class DocumentSigning
{
    [Inject] private IDocumentAssignmentsAppService DocumentAssignmentsAppService { get; set; } = default!;
    [Inject] private IMasterDatasAppService MasterDatasAppService { get; set; } = default!;
    [Inject] private IUserSignaturesAppService UserSignaturesAppService { get; set; } = default!;
    [Parameter] public Guid? NotificationRelatedId { get; set; }

    #region Properties

    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new();
    protected PageToolbar Toolbar { get; } = new PageToolbar();

    // Filter properties
    // BUG-4 FIX: Initialized in OnAfterRenderAsync using Clock.Now (IClock) for timezone consistency
    private DateTime? FromDate { get; set; }
    private DateTime? ToDate { get; set; }
    private string? FilterText { get; set; }
    private DocumentSigningFilterMode CurrentFilterMode { get; set; } = DocumentSigningFilterMode.All;

    // Data grid
    private List<DocumentSigningItemDto> DocumentSigningList { get; set; } = new();
    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    private int CurrentPage { get; set; } = 1;
    private string CurrentSorting { get; set; } = string.Empty;
    private int TotalCount { get; set; }
    private bool IsLoading { get; set; }

    // Counts for left panel
    private int AllCount { get; set; }
    private int SentToMeCount { get; set; }
    private int SentByMeCount { get; set; }
    private int FollowingCount { get; set; }

    // Submit Workflow Modal (reusable component)
    private HC.Blazor.Components.SubmitWorkflowModal.SubmitWorkflowModal SubmitWorkflowModalRef { get; set; } = default!;

    // My documents list - used by Resubmit modal for document selection
    private List<DocumentWithNavigationPropertiesDto> MyDocumentsList { get; set; } = new();

    // Action Modal
    private Modal WorkflowActionModal { get; set; } = new();
    private DocumentSigningItemDto? SelectedDocumentForAction { get; set; }
    private string SelectedAction { get; set; } = nameof(WorkflowInstanceLogAction.APPROVE);
    private string? ActionNote { get; set; }
    private RichTextEdit? ActionNoteEditorRef { get; set; }

    // Action Modal - Tabs, Logs, Files, DocumentHistory
    private string ActionModalActiveTab { get; set; } = "general"; // "general" | "workflowHistory"
    private List<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto> WorkflowLogs { get; set; } = new();
    private List<DocumentWorkflowInstanceFileWithNavigationPropertiesDto> WorkflowFiles { get; set; } = new();
    private List<DocumentHistoryWithNavigationPropertiesDto> DocumentHistories { get; set; } = new();
    private bool IsLoadingLogs { get; set; }
    private bool IsLoadingFiles { get; set; }
    private bool IsLoadingHistories { get; set; }

    // Overdue & AllowReturn state for Action Modal
    private bool IsOverdue { get; set; }
    private bool AllowReturnAction { get; set; }

    // View-only mode for Action Modal (no actions allowed)
    private bool IsViewOnly { get; set; }

    // Signing Methods (MasterData Type = "LOAI_KY")
    private List<MasterDataDto> SigningMethods { get; set; } = new();
    private Guid? SelectedSigningMethodId { get; set; }
    private List<UserSignatureWithNavigationPropertiesDto> AvailableUserSignaturesForMethod { get; set; } = new();
    private Guid? SelectedUserSignatureId { get; set; }

    // Signing Document Files (from DocumentAssignments.DocumentFileResultId)
    private List<DocumentAssignmentWithNavigationPropertiesDto> SigningDocumentAssignments { get; set; } = new();
    private bool IsLoadingSigningDocuments { get; set; }

    // Current Step Detail (loaded from WorkflowStepTemplates + WorkflowStepAssignments)
    private WorkflowStepDetailDto? CurrentStepDetailInfo { get; set; }
    private DocumentWorkflowInstanceDto? WorkflowInstanceInfo { get; set; }

    // All workflow steps with their signing status (for action modal step overview)
    private List<WorkflowStepStatusDto> AllStepsWithStatus { get; set; } = new();

    // Debounce
    private CancellationTokenSource? SearchDebounceCts { get; set; }
    private bool IsInitialDataLoaded { get; set; }
    private Guid? LastNotificationRelatedId { get; set; }
    private bool HasTriedAutoOpenFromNotification { get; set; }

    // PDF Viewer Modal
    private Modal DocumentPdfViewerModal { get; set; } = new();
    private string? DocumentPdfFileUrl { get; set; }
    private bool IsDocumentPdfFile { get; set; }
    private Guid? CurrentDocumentPdfDocumentId { get; set; }
    private HC.Blazor.Components.ProjectTaskCreateModal.ProjectTaskCreateModal CreateTaskModalRef { get; set; } = default!;

    // Resubmit Returned Workflow Modal
    private Modal ResubmitWorkflowModal { get; set; } = new();
    private ReturnedWorkflowInfoDto? ReturnedWorkflowInfo { get; set; }
    private string? ResubmitSigningContent { get; set; }
    private RichTextEdit? ResubmitSigningContentEditorRef { get; set; }
    private bool ResubmitUseWorkflowTemplateFile { get; set; }
    private Guid? ResubmitSelectedDocumentId { get; set; }
    private DocumentWithNavigationPropertiesDto? ResubmitSelectedDocumentDto { get; set; }
    private FilePicker? ResubmitFilePicker { get; set; }
    private List<UploadedFileInfo> ResubmitUploadedFiles { get; set; } = new();
    private List<AttachedFileDto> ResubmitExistingFiles { get; set; } = new();
    private List<Guid> ResubmitDeleteFileIds { get; set; } = new();
    private int ResubmitModalResetKey { get; set; }

    /// <summary>
    /// Defer RichTextEdit mount until modal is shown so Quill initializes in a visible DOM (avoids Blazorise dispose NRE).
    /// </summary>
    private bool _showActionModalRichTextEditors;
    private int _actionModalEditorSessionKey;
    private bool _showResubmitModalRichTextEditors;
    private int _resubmitModalEditorSessionKey;

    #endregion

    #region Inner Classes

    /// <summary>
    /// Tracks uploaded files for the workflow submit modal
    /// </summary>
    private class UploadedFileInfo
    {
        public Guid DocumentFileId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }

    #endregion

    #region Initialization

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // BUG-4 FIX: Initialize filter dates using Clock.Now (IClock) for timezone consistency
            FromDate = Clock.Now.AddDays(-60);
            ToDate = Clock.Now;

            BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["DocumentSigning"]));
            await SetToolbarItemsAsync();
            await LoadDocumentSigningListAsync();
            IsInitialDataLoaded = true;
            await TryAutoOpenActionModalFromNotificationAsync();
            await InvokeAsync(StateHasChanged);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (NotificationRelatedId != LastNotificationRelatedId)
        {
            LastNotificationRelatedId = NotificationRelatedId;
            HasTriedAutoOpenFromNotification = false;

            if (IsInitialDataLoaded)
            {
                await TryAutoOpenActionModalFromNotificationAsync();
            }
        }
    }

    private Task SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["CreateSigning"], async () =>
        {
            await ShowSubmitWorkflowModalAsync();
        }, IconName.Add, requiredPolicyName: HCPermissions.Documents.SubmitForSigning);

        return Task.CompletedTask;
    }

    #endregion

    #region Data Loading

    private async Task LoadDocumentSigningListAsync()
    {
        IsLoading = true;
        try
        {
            var input = new GetDocumentSigningListInput
            {
                FilterText = FilterText,
                FilterMode = CurrentFilterMode,
                FromDate = FromDate,
                ToDate = ToDate,
                MaxResultCount = PageSize,
                SkipCount = (CurrentPage - 1) * PageSize,
                Sorting = CurrentSorting
            };

            var result = await DocumentWorkflowInstancesAppService.GetDocumentSigningListAsync(input);

            DocumentSigningList = result.Items;
            TotalCount = (int)result.TotalCount;
            AllCount = result.AllCount;
            SentToMeCount = result.SentToMeCount;
            SentByMeCount = result.SentByMeCount;
            FollowingCount = result.FollowingCount;

            if (IsInitialDataLoaded)
            {
                await TryAutoOpenActionModalFromNotificationAsync();
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Load personal documents (SourceType=1) for document selection in submit modal
    /// </summary>
    private async Task LoadMyDocumentsAsync()
    {
        try
        {
            var result = await DocumentsAppService.GetListAsync(new GetDocumentsInput
            {
                SourceType = DocumentSourceType.Personal,
                CreatorId = CurrentUser.Id,
                MaxResultCount = 200,
                SkipCount = 0
            });
            MyDocumentsList = result.Items.ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading personal documents");
        }
    }

    /// <summary>
    /// Load workflow instance logs for the action modal
    /// </summary>
    private async Task LoadWorkflowLogsAsync(Guid workflowInstanceId)
    {
        IsLoadingLogs = true;
        try
        {
            WorkflowLogs = await DocumentWorkflowInstancesAppService.GetWorkflowInstanceLogsAsync(workflowInstanceId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading workflow logs for instance {InstanceId}", workflowInstanceId);
            WorkflowLogs = new();
        }
        finally
        {
            IsLoadingLogs = false;
        }
    }

    /// <summary>
    /// Load workflow instance files for the action modal
    /// </summary>
    private async Task LoadWorkflowFilesAsync(Guid workflowInstanceId)
    {
        IsLoadingFiles = true;
        try
        {
            WorkflowFiles = await DocumentWorkflowInstancesAppService.GetWorkflowInstanceFilesAsync(workflowInstanceId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading workflow files for instance {InstanceId}", workflowInstanceId);
            WorkflowFiles = new();
        }
        finally
        {
            IsLoadingFiles = false;
        }
    }

    /// <summary>
    /// Load document histories for the action modal
    /// </summary>
    private async Task LoadDocumentHistoriesAsync(Guid documentId)
    {
        IsLoadingHistories = true;
        try
        {
            DocumentHistories = await DocumentWorkflowInstancesAppService.GetDocumentHistoriesByDocumentIdAsync(documentId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading document histories for document {DocumentId}", documentId);
            DocumentHistories = new();
        }
        finally
        {
            IsLoadingHistories = false;
        }
    }

    /// <summary>
    /// Load signing methods from MasterData (Type = "LOAI_KY")
    /// </summary>
    private async Task LoadSigningMethodsAsync()
    {
        try
        {
            var result = await MasterDatasAppService.GetListAsync(new GetMasterDatasInput
            {
                Type = "LOAI_KY",
                IsActive = true,
                MaxResultCount = 100
            });
            SigningMethods = result.Items.ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading signing methods");
            SigningMethods = new();
        }
    }

    /// <summary>
    /// Load signing document files from DocumentAssignments that have DocumentFileResultId.
    /// Shows the latest processed files for the document workflow.
    /// </summary>
    private async Task LoadSigningDocumentFilesAsync(Guid documentId)
    {
        IsLoadingSigningDocuments = true;
        try
        {
            var result = await DocumentAssignmentsAppService.GetListAsync(new GetDocumentAssignmentsInput
            {
                DocumentId = documentId,
                MaxResultCount = 100,
                SkipCount = 0
            });

            // Filter assignments that have DocumentFileResultId and sort by creation date desc
            SigningDocumentAssignments = result.Items
                .Where(a => a.DocumentAssignment.DocumentFileResultId.HasValue && a.DocumentFileResult != null)
                .OrderByDescending(a => a.DocumentAssignment.CreationTime)
                .ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading signing document files for document {DocumentId}", documentId);
            SigningDocumentAssignments = new();
        }
        finally
        {
            IsLoadingSigningDocuments = false;
        }
    }

    /// <summary>
    /// Load current step detail info (step name, assigned users, SLA) and workflow instance (StartedAt, FinishedAt).
    /// Uses GetAsync to get the instance, then GetWorkflowSubmitInfoAsync to get step details with assigned users.
    /// </summary>
    private async Task LoadCurrentStepDetailAsync(Guid workflowInstanceId)
    {
        try
        {
            WorkflowInstanceInfo = await DocumentWorkflowInstancesAppService.GetAsync(workflowInstanceId);

            if (WorkflowInstanceInfo != null)
            {
                var submitInfo = await DocumentWorkflowInstancesAppService.GetWorkflowSubmitInfoAsync(WorkflowInstanceInfo.WorkflowId);
                CurrentStepDetailInfo = submitInfo.Steps.FirstOrDefault(s => s.StepId == WorkflowInstanceInfo.CurrentStepId);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading current step detail for workflow instance {InstanceId}", workflowInstanceId);
            CurrentStepDetailInfo = null;
            WorkflowInstanceInfo = null;
        }
    }

    /// <summary>
    /// Load all workflow steps with their signing status for the action modal step overview.
    /// </summary>
    private async Task LoadAllStepsWithStatusAsync(Guid workflowInstanceId)
    {
        try
        {
            AllStepsWithStatus = await DocumentWorkflowInstancesAppService.GetAllStepsWithStatusAsync(workflowInstanceId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading all steps with status for workflow instance {InstanceId}", workflowInstanceId);
            AllStepsWithStatus = new();
        }
    }

    #endregion

    #region Filter Events

    private async Task OnFromDateChanged(DateTime? date)
    {
        FromDate = date;
        await SearchAsync();
    }

    private async Task OnToDateChanged(DateTime? date)
    {
        ToDate = date;
        await SearchAsync();
    }

    private async Task OnFilterTextChanged(string? text)
    {
        FilterText = text;
        await DebouncedSearchAsync();
    }

    private async Task OnFilterModeChanged(DocumentSigningFilterMode mode)
    {
        CurrentFilterMode = mode;
        CurrentPage = 1;
        await LoadDocumentSigningListAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task SearchAsync()
    {
        CurrentPage = 1;
        await LoadDocumentSigningListAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task DebouncedSearchAsync()
    {
        var previous = SearchDebounceCts;
        SearchDebounceCts = new CancellationTokenSource();
        previous?.Cancel();
        previous?.Dispose();
        var token = SearchDebounceCts.Token;

        try
        {
            await Task.Delay(350, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested) return;
        await SearchAsync();
    }

    private async Task OnDataGridReadAsync(DataGridReadDataEventArgs<DocumentSigningItemDto> e)
    {
        CurrentSorting = e.Columns
            .Where(c => c.SortDirection != SortDirection.Default)
            .Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : ""))
            .JoinAsString(",");
        CurrentPage = e.Page;
        await LoadDocumentSigningListAsync();
        await InvokeAsync(StateHasChanged);
    }

    #endregion

    #region Submit Workflow Modal (Reusable Component)

    private async Task ShowSubmitWorkflowModalAsync()
    {
        if (SubmitWorkflowModalRef != null)
        {
            await SubmitWorkflowModalRef.ShowAsync(preSelectedDocument: null);
        }
    }

    private async Task OnSubmitWorkflowCompletedAsync()
    {
        await LoadDocumentSigningListAsync();
        await InvokeAsync(StateHasChanged);
    }

    private Task OnSubmitWorkflowClosedAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Workflow Action Modal

    private async Task TryAutoOpenActionModalFromNotificationAsync()
    {
        if (HasTriedAutoOpenFromNotification || !NotificationRelatedId.HasValue)
        {
            return;
        }

        HasTriedAutoOpenFromNotification = true;

        try
        {
            var relatedId = NotificationRelatedId.Value;
            var targetDocument = DocumentSigningList.FirstOrDefault(x =>
                (x.WorkflowInstanceId.HasValue && x.WorkflowInstanceId.Value == relatedId)
                || x.DocumentId == relatedId);

            if (targetDocument == null)
            {
                targetDocument = await FetchSigningItemForNotificationAsync(relatedId);
            }

            if (targetDocument == null)
            {
                Logger.LogInformation(
                    "Auto-open workflow modal skipped: related id {RelatedId} not found in signing list.",
                    relatedId);
                await UiMessageService.Warn(L["WorkflowNotificationItemNotFound"],
                    options: new Action<UiMessageOptions>(o => o.OkButtonText = L["Ok"]));
                return;
            }

            var canProcess = targetDocument.CanAct
                && targetDocument.MyAssignmentId.HasValue
                && string.Equals(targetDocument.MyAssignmentStatus, nameof(DocumentAssignmentStatus.PENDING), StringComparison.OrdinalIgnoreCase);

            await ShowActionModalAsync(targetDocument, viewOnly: !canProcess);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Auto-open workflow modal from notification failed.");
            await HandleErrorAsync(ex);
        }
    }

    /// <summary>
    /// Loads a single signing row by document or workflow id (full list scope, no date filter) for notification deep links.
    /// </summary>
    private async Task<DocumentSigningItemDto?> FetchSigningItemForNotificationAsync(Guid relatedId)
    {
        var input = new GetDocumentSigningListInput
        {
            FilterMode = DocumentSigningFilterMode.All,
            FromDate = null,
            ToDate = null,
            FilterText = null,
            FocusDocumentId = relatedId,
            MaxResultCount = 1,
            SkipCount = 0
        };
        var result = await DocumentWorkflowInstancesAppService.GetDocumentSigningListAsync(input);
        return result.Items.FirstOrDefault();
    }

    private async Task ShowActionModalAsync(DocumentSigningItemDto document, bool viewOnly = false)
    {
        var loadSucceeded = false;
        _showActionModalRichTextEditors = false;
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            SelectedDocumentForAction = document;
            SelectedAction = nameof(WorkflowInstanceLogAction.APPROVE);
            ActionNote = null;
            ActionModalActiveTab = "general";
            WorkflowLogs = new();
            WorkflowFiles = new();
            DocumentHistories = new();
            SigningDocumentAssignments = new();
            CurrentStepDetailInfo = null;
            WorkflowInstanceInfo = null;
            AllStepsWithStatus = new();
            IsOverdue = false;
            AllowReturnAction = false;
            IsViewOnly = viewOnly;
            SelectedSigningMethodId = null;
            AvailableUserSignaturesForMethod = new();
            SelectedUserSignatureId = null;

            // M3: pull everything the modal needs in a single bundle call instead of 7 parallel HTTPs.
            // `LoadSigningDocumentFilesAsync` stays separate because it comes from DocumentAssignmentsAppService.
            if (document.WorkflowInstanceId.HasValue)
            {
                try
                {
                    var bundle = await DocumentWorkflowInstancesAppService.GetActionBundleAsync(
                        new GetWorkflowInstanceActionBundleInput
                        {
                            WorkflowInstanceId = document.WorkflowInstanceId.Value,
                            DocumentId = document.DocumentId,
                            SigningMethodsMaxResultCount = 100
                        });

                    WorkflowInstanceInfo = bundle.Instance;
                    CurrentStepDetailInfo = bundle.CurrentStepDetail;
                    WorkflowLogs = bundle.Logs ?? new();
                    WorkflowFiles = bundle.Files ?? new();
                    DocumentHistories = bundle.DocumentHistories ?? new();
                    AllStepsWithStatus = bundle.AllStepsWithStatus ?? new();
                    SigningMethods = bundle.SigningMethods ?? new();

                    // Files list from document assignments is served by a different AppService;
                    // fire it off in the background so it doesn't block the modal opening.
                    await LoadSigningDocumentFilesAsync(document.DocumentId);
                }
                catch (Exception ex)
                {
                    // Fallback to the legacy per-call path if the bundle endpoint fails.
                    Logger.LogWarning(ex, "Signing action-bundle failed; falling back to per-call loads");
                    var tasks = new List<Task>
                    {
                        LoadWorkflowLogsAsync(document.WorkflowInstanceId.Value),
                        LoadWorkflowFilesAsync(document.WorkflowInstanceId.Value),
                        LoadCurrentStepDetailAsync(document.WorkflowInstanceId.Value),
                        LoadAllStepsWithStatusAsync(document.WorkflowInstanceId.Value),
                        LoadDocumentHistoriesAsync(document.DocumentId),
                        LoadSigningMethodsAsync(),
                        LoadSigningDocumentFilesAsync(document.DocumentId)
                    };
                    await Task.WhenAll(tasks);
                }
            }
            else
            {
                var tasks = new List<Task>
                {
                    LoadDocumentHistoriesAsync(document.DocumentId),
                    LoadSigningMethodsAsync(),
                    LoadSigningDocumentFilesAsync(document.DocumentId)
                };
                await Task.WhenAll(tasks);
            }

            // Default signing method to ELECTRONIC when available.
            var defaultSigningMethod = SigningMethods.FirstOrDefault(m => m.Code == nameof(SignType.ELECTRONIC));
            if (defaultSigningMethod != null)
            {
                await OnSigningMethodChangedAsync(defaultSigningMethod.Id);
            }

            // Check overdue and get AllowReturn for current step
            if (document.WorkflowInstanceId.HasValue)
            {
                try
                {
                    var overdueResult = await DocumentWorkflowInstancesAppService
                        .CheckAndHandleOverdueAsync(document.WorkflowInstanceId.Value);
                    IsOverdue = overdueResult.IsOverdue;
                    AllowReturnAction = overdueResult.AllowReturn;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error checking overdue for workflow instance {InstanceId}", document.WorkflowInstanceId.Value);
                }
            }

            loadSucceeded = true;
        }
        catch (Exception ex)
        {
            SelectedDocumentForAction = null;
            _showActionModalRichTextEditors = false;
            await HandleErrorAsync(ex);
        }
        finally
        {
            await BlockUiService.UnBlock();
            if (loadSucceeded)
            {
                _actionModalEditorSessionKey++;
                await InvokeAsync(WorkflowActionModal.Show);
                await Task.Delay(100);
                _showActionModalRichTextEditors = true;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task HideWorkflowActionModalAsync()
    {
        _showActionModalRichTextEditors = false;
        await InvokeAsync(StateHasChanged);
        await Task.Delay(50);
        await InvokeAsync(WorkflowActionModal.Hide);
    }

    private async Task CloseActionModalAsync()
    {
        await HideWorkflowActionModalAsync();
    }

    private async Task ConfirmWorkflowActionAsync()
    {
        var isBlocked = false;
        try
        {
            if (SelectedDocumentForAction == null || string.IsNullOrEmpty(SelectedAction))
            {
                await UiMessageService.Error(L["PleaseSelectAction"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            if (!SelectedDocumentForAction.WorkflowInstanceId.HasValue || !SelectedDocumentForAction.MyAssignmentId.HasValue)
            {
                await UiMessageService.Error(L["NoActiveAssignment"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            // Validate signing method is selected when approving
            if (SelectedAction == nameof(WorkflowInstanceLogAction.APPROVE) && !SelectedSigningMethodId.HasValue)
            {
                await UiMessageService.Error(L["PleaseSelectSigningMethod"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            if (SelectedAction == nameof(WorkflowInstanceLogAction.APPROVE)
                && AvailableUserSignaturesForMethod.Count > 1
                && !SelectedUserSignatureId.HasValue)
            {
                await UiMessageService.Error(L["PleaseSelectUserSignature"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            // Confirmation message based on action
            var confirmMessage = SelectedAction switch
            {
                nameof(WorkflowInstanceLogAction.APPROVE) => L["ConfirmApprove"],
                nameof(WorkflowInstanceLogAction.RETURN) => L["ConfirmReturn"],
                nameof(WorkflowInstanceLogAction.REJECT) => L["ConfirmReject"],
                _ => L["ConfirmAction"]
            };

            var confirmed = await UiMessageService.Confirm(confirmMessage);
            if (!confirmed) return;

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            isBlocked = true;

            // Blazor binding updates on blur; get note from editor to ensure we have latest content for <<NoteContentXX>>
            var actionNote = ActionNote?.Trim();
            if (ActionNoteEditorRef != null)
            {
                var editorHtml = await ActionNoteEditorRef.GetHtmlAsync();
                if (!string.IsNullOrWhiteSpace(editorHtml))
                {
                    actionNote = editorHtml.Trim();
                }
            }

            var input = new WorkflowActionInput
            {
                DocumentWorkflowInstanceId = SelectedDocumentForAction.WorkflowInstanceId.Value,
                DocumentAssignmentId = SelectedDocumentForAction.MyAssignmentId.Value,
                Action = SelectedAction,
                Note = NormalizeRichTextHtml(actionNote),
                SigningMethodId = SelectedSigningMethodId,
                UserSignatureId = SelectedUserSignatureId
            };

            await DocumentWorkflowInstancesAppService.ProcessWorkflowActionAsync(input);

            // Success message based on action
            var successMessage = SelectedAction switch
            {
                nameof(WorkflowInstanceLogAction.APPROVE) => L["DocumentApprovedSuccessfully"],
                nameof(WorkflowInstanceLogAction.RETURN) => L["DocumentReturnedSuccessfully"],
                nameof(WorkflowInstanceLogAction.REJECT) => L["DocumentRejectedSuccessfully"],
                _ => L["ActionCompletedSuccessfully"]
            };

            await BlockUiService.UnBlock();
            isBlocked = false;

            await UiMessageService.Success(successMessage,
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            await HideWorkflowActionModalAsync();
            await LoadDocumentSigningListAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            if (isBlocked)
            {
                await BlockUiService.UnBlock();
                isBlocked = false;
            }

            await HandleErrorAsync(ex);
        }
        finally
        {
            if (isBlocked)
            {
                await BlockUiService.UnBlock();
            }
        }
    }

    #endregion

    #region Resubmit Returned Workflow Modal

    /// <summary>
    /// Show the resubmit modal for a returned workflow.
    /// Pre-populates with data from the returned workflow instance.
    /// </summary>
    private async Task ShowResubmitModalAsync(DocumentSigningItemDto document)
    {
        var loadSucceeded = false;
        _showResubmitModalRichTextEditors = false;
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);

            // Reset resubmit modal state
            ReturnedWorkflowInfo = null;
            ResubmitSigningContent = null;
            ResubmitUseWorkflowTemplateFile = false;
            ResubmitSelectedDocumentId = null;
            ResubmitSelectedDocumentDto = null;
            ResubmitUploadedFiles.Clear();
            ResubmitExistingFiles.Clear();
            ResubmitDeleteFileIds.Clear();
            ResubmitModalResetKey++;

            if (ResubmitFilePicker != null)
            {
                await ResubmitFilePicker.Clear();
            }

            // Load personal documents for selection
            await LoadMyDocumentsAsync();

            // Load returned workflow info
            if (document.WorkflowInstanceId.HasValue)
            {
                ReturnedWorkflowInfo = await DocumentWorkflowInstancesAppService
                    .GetReturnedWorkflowInfoAsync(document.WorkflowInstanceId.Value);

                if (ReturnedWorkflowInfo != null)
                {
                    // Pre-populate with previous data
                    ResubmitSigningContent = ReturnedWorkflowInfo.LastSigningContent;
                    ResubmitExistingFiles = ReturnedWorkflowInfo.AttachedFiles.ToList();
                    ResubmitSelectedDocumentId = ReturnedWorkflowInfo.DocumentId;
                    ResubmitSelectedDocumentDto = MyDocumentsList
                        .FirstOrDefault(d => d.Document.Id == ReturnedWorkflowInfo.DocumentId);
                }
            }

            loadSucceeded = true;
        }
        catch (Exception ex)
        {
            ReturnedWorkflowInfo = null;
            _showResubmitModalRichTextEditors = false;
            await HandleErrorAsync(ex);
        }
        finally
        {
            await BlockUiService.UnBlock();
            if (loadSucceeded)
            {
                _resubmitModalEditorSessionKey++;
                await InvokeAsync(ResubmitWorkflowModal.Show);
                await Task.Delay(100);
                _showResubmitModalRichTextEditors = true;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task HideResubmitWorkflowModalAsync()
    {
        _showResubmitModalRichTextEditors = false;
        await InvokeAsync(StateHasChanged);
        await Task.Delay(50);
        await InvokeAsync(ResubmitWorkflowModal.Hide);
    }

    private async Task CloseResubmitModalAsync()
    {
        await HideResubmitWorkflowModalAsync();
    }

    private void OnResubmitDocumentSelected(Guid? documentId)
    {
        ResubmitSelectedDocumentId = documentId;
        ResubmitSelectedDocumentDto = documentId.HasValue
            ? MyDocumentsList.FirstOrDefault(d => d.Document.Id == documentId.Value)
            : null;
    }

    private void OnResubmitUseWorkflowTemplateFileChanged(bool value)
    {
        ResubmitUseWorkflowTemplateFile = value;
        if (value)
        {
            ResubmitSelectedDocumentId = null;
            ResubmitSelectedDocumentDto = null;
        }
    }

    /// <summary>
    /// Remove an existing attached file (mark for deletion)
    /// </summary>
    private void RemoveExistingFile(AttachedFileDto file)
    {
        ResubmitDeleteFileIds.Add(file.FileId);
        ResubmitExistingFiles.Remove(file);
    }

    /// <summary>
    /// Upload handler for resubmit modal
    /// </summary>
    private async Task OnResubmitFileUpload(FileUploadEventArgs e)
    {
        try
        {
            using var memoryStream = new MemoryStream();
            await e.File.OpenReadStream(long.MaxValue).CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            var extension = Path.GetExtension(e.File.Name);
            var filePath = $"workflow-files/{Guid.NewGuid()}{extension}";

            using var uploadStream = new MemoryStream(fileBytes);
            await BlobContainer.SaveAsync(filePath, uploadStream);

            var documentFileDto = await DocumentFilesAppService.CreateAsync(new DocumentFileCreateDto
            {
                Name = e.File.Name,
                Path = filePath,
                Hash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(fileBytes)),
                IsSigned = false,
                UploadedAt = Clock.Now, // BUG-4 FIX: use Clock.Now instead of DateTime.Now
            });

            ResubmitUploadedFiles.Add(new UploadedFileInfo
            {
                DocumentFileId = documentFileDto.Id,
                Name = e.File.Name,
                Path = filePath
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error uploading file {FileName} in resubmit modal", e.File.Name);
            await HandleErrorAsync(ex);
        }
        finally
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>
    /// Confirm and submit the re-submit workflow
    /// </summary>
    private async Task ConfirmResubmitWorkflowAsync()
    {
        try
        {
            if (ReturnedWorkflowInfo == null)
            {
                await UiMessageService.Error(L["WorkflowNotReturned"],
                    options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            // Validate document selection (when not using template file)
            if (!ResubmitUseWorkflowTemplateFile && !ResubmitSelectedDocumentId.HasValue)
            {
                await UiMessageService.Error(L["The {0} field is required.", L["Document"]],
                    options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            var confirmed = await UiMessageService.Confirm(L["ConfirmResubmitForSigning"]);
            if (!confirmed) return;

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);

            // Blazor binding updates on blur; get value directly from editor to ensure we have latest content.
            var resubmitSigningContent = ResubmitSigningContent?.Trim();
            if (ResubmitSigningContentEditorRef != null)
            {
                var editorHtml = await ResubmitSigningContentEditorRef.GetHtmlAsync();
                if (!string.IsNullOrWhiteSpace(editorHtml))
                {
                    resubmitSigningContent = editorHtml.Trim();
                }
            }

            var input = new ResubmitReturnedWorkflowInput
            {
                ReturnedWorkflowInstanceId = ReturnedWorkflowInfo.WorkflowInstanceId,
                UseWorkflowTemplateFile = ResubmitUseWorkflowTemplateFile,
                DocumentFileId = null, // Will be resolved from new document
                NewDocumentId = ResubmitUseWorkflowTemplateFile ? null : ResubmitSelectedDocumentId,
                SigningContent = resubmitSigningContent,
                AttachedFileIds = ResubmitUploadedFiles.Any()
                    ? ResubmitUploadedFiles.Select(f => f.DocumentFileId).ToList()
                    : null,
                DeleteFileIds = ResubmitDeleteFileIds.Any() ? ResubmitDeleteFileIds : null
            };

            await DocumentWorkflowInstancesAppService.ResubmitReturnedWorkflowAsync(input);

            await UiMessageService.Success(L["WorkflowResubmittedSuccessfully"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            await HideResubmitWorkflowModalAsync();
            await LoadDocumentSigningListAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            await BlockUiService.UnBlock();
        }
    }

    #endregion

    #region Helper Methods

    private string GetFilterItemClass(DocumentSigningFilterMode mode)
    {
        return CurrentFilterMode == mode
            ? "list-group-item-action active cursor-pointer"
            : "list-group-item-action cursor-pointer";
    }

    private string GetWorkflowStatusBadgeClass(string status) => status switch
    {
        nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS) => "bg-info text-white",
        nameof(DocumentWorkflowInstanceStatus.COMPLETED) => "bg-success text-white",
        nameof(DocumentWorkflowInstanceStatus.REJECTED) => "bg-danger text-white",
        nameof(DocumentWorkflowInstanceStatus.RETURNED) => "bg-warning text-dark",
        nameof(DocumentWorkflowInstanceStatus.CANCELLED) => "bg-secondary text-white",
        nameof(DocumentWorkflowInstanceStatus.DRAFT) => "bg-light text-dark",
        _ => "bg-secondary text-white"
    };

    private string GetAssignmentStatusBadgeClass(string status) => status switch
    {
        nameof(DocumentAssignmentStatus.PENDING) => "bg-warning text-dark",
        nameof(DocumentAssignmentStatus.DONE) => "bg-success text-white",
        nameof(DocumentAssignmentStatus.REJECTED) => "bg-danger text-white",
        nameof(DocumentAssignmentStatus.REVOKE) => "bg-secondary text-white",
        _ => "bg-secondary text-white"
    };

    private Color GetActionButtonColor() => SelectedAction switch
    {
        nameof(WorkflowInstanceLogAction.APPROVE) => Color.Success,
        nameof(WorkflowInstanceLogAction.RETURN) => Color.Warning,
        nameof(WorkflowInstanceLogAction.REJECT) => Color.Danger,
        _ => Color.Primary
    };

    private string GetLogActionBadgeClass(string action) => action switch
    {
        nameof(WorkflowInstanceLogAction.SUBMIT_WORKFLOW) => "bg-primary text-white",
        nameof(WorkflowInstanceLogAction.APPROVE) => "bg-success text-white",
        nameof(WorkflowInstanceLogAction.RETURN) => "bg-warning text-dark",
        nameof(WorkflowInstanceLogAction.REJECT) => "bg-danger text-white",
        nameof(WorkflowInstanceLogAction.SIGN) => "bg-success text-white",
        _ => "bg-secondary text-white"
    };

    private string GetLogActionIcon(string action) => action switch
    {
        nameof(WorkflowInstanceLogAction.SUBMIT_WORKFLOW) => "bi bi-play-circle-fill",
        nameof(WorkflowInstanceLogAction.APPROVE) => "bi bi-check-circle-fill",
        nameof(WorkflowInstanceLogAction.RETURN) => "bi bi-arrow-return-left",
        nameof(WorkflowInstanceLogAction.REJECT) => "bi bi-x-circle-fill",
        nameof(WorkflowInstanceLogAction.SIGN) => "bi bi-pen-fill",
        _ => "bi bi-circle"
    };

    private async Task OnSigningMethodChangedAsync(Guid? signingMethodId)
    {
        try
        {
            SelectedSigningMethodId = signingMethodId;
            SelectedUserSignatureId = null;
            AvailableUserSignaturesForMethod = new();

            if (!signingMethodId.HasValue || !CurrentUser.Id.HasValue)
            {
                return;
            }

            var selectedMethod = SigningMethods.FirstOrDefault(m => m.Id == signingMethodId.Value);
            if (selectedMethod == null
                || (selectedMethod.Code != nameof(SignType.ELECTRONIC) && selectedMethod.Code != nameof(SignType.DIGITAL)))
            {
                return;
            }

            var result = await UserSignaturesAppService.GetListAsync(new GetUserSignaturesInput
            {
                IdentityUserId = CurrentUser.Id.Value,
                SignType = selectedMethod.Code,
                IsActive = true,
                MaxResultCount = 100,
                SkipCount = 0,
            Sorting = "UserSignature.ValidTo desc"
            });

            var now = Clock.Now;
            AvailableUserSignaturesForMethod = result.Items
                .Where(x =>
                    (!x.UserSignature.ValidFrom.HasValue || x.UserSignature.ValidFrom.Value <= now)
                    && (!x.UserSignature.ValidTo.HasValue || x.UserSignature.ValidTo.Value >= now))
                .ToList();

            if (AvailableUserSignaturesForMethod.Count == 1)
            {
                SelectedUserSignatureId = AvailableUserSignaturesForMethod[0].UserSignature.Id;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading user signatures for signing method: {SigningMethodId}", signingMethodId);
            AvailableUserSignaturesForMethod = new();
            SelectedUserSignatureId = null;
            await HandleErrorAsync(ex);
        }
    }

    private string GetUserSignatureDisplayName(UserSignatureWithNavigationPropertiesDto item)
    {
        var providerCode = item.UserSignature.ProviderCode;
        var validTo = item.UserSignature.ValidTo?.ToString("dd/MM/yyyy") ?? "--";
        return $"{providerCode} (ValidTo: {validTo})";
    }

    private static string? GetWorkflowTemplateFilePath(WorkflowSubmitInfoDto? workflowInfo)
    {
        return !string.IsNullOrWhiteSpace(workflowInfo?.WordTemplatePath)
            ? workflowInfo.WordTemplatePath
            : workflowInfo?.PdfTemplatePath;
    }

    private static string GetWorkflowTemplateFileName(WorkflowSubmitInfoDto? workflowInfo)
    {
        var path = GetWorkflowTemplateFilePath(workflowInfo);
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : Path.GetFileName(path);
    }

    private static string? NormalizeRichTextHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var normalized = html.Trim();
        var plainText = Regex.Replace(normalized, "<[^>]+>", string.Empty);
        plainText = WebUtility.HtmlDecode(plainText)
            .Replace('\u00A0', ' ')
            .Trim();

        return string.IsNullOrWhiteSpace(plainText) ? null : normalized;
    }

    private static MarkupString ToMarkupString(string? html)
    {
        return new MarkupString(NormalizeRichTextHtml(html) ?? string.Empty);
    }

    private void NavigateToDocumentDetail(Guid documentId)
    {
        NavigationManager.NavigateTo($"/document-detail/{documentId}");
    }

    private string FilePickerLocalizer(string name, params object[] arguments)
    {
        return name switch
        {
            "ClearConfirmation" => L["FilePicker:ClearConfirmation"],
            "Clear" => L["Clear"],
            "Cancel" => L["Cancel"],
            "Confirm" => L["Confirm"],
            "Are you sure you want to clear all files?" => L["FilePicker:ClearConfirmation"],
            "Are you sure you want to clear the selected files?" => L["FilePicker:ClearConfirmation"],
            _ => L[name] ?? name
        };
    }

    #endregion

    #region PDF Viewer Modal

    /// <summary>
    /// Open PDF viewer modal for a document signing item.
    /// Logic: Get DocumentFiles by DocumentId (same approach as DocumentDetail),
    /// find the first PDF file and display it. If no original file found,
    /// fallback to checking DocumentAssignment's DocumentFileResultId.
    /// </summary>
    private async Task OpenDocumentPdfViewerModalAsync(DocumentSigningItemDto item)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            CurrentDocumentPdfDocumentId = item.DocumentId;

            string? pdfFilePath = null;

            var assignmentsResult = await DocumentAssignmentsAppService.GetListAsync(new GetDocumentAssignmentsInput
            {
                DocumentId = item.DocumentId,
                MaxResultCount = 1,
                SkipCount = 0,
                Sorting = "DocumentAssignment.CreationTime desc"
            });

          
            if (assignmentsResult != null && assignmentsResult.Items.Any())
            {
                 var assignmentWithFile = assignmentsResult.Items
                    .FirstOrDefault(a => a.DocumentAssignment.DocumentFileResultId.HasValue
                        && a.DocumentFileResult != null
                        && !string.IsNullOrEmpty(a.DocumentFileResult.Path)
                        && HC.Blazor.Shared.FileHelper.IsPdfFileExtension(a.DocumentFileResult.Name));

                if (assignmentWithFile != null)
                {
                    pdfFilePath = assignmentWithFile.DocumentFileResult!.Path;
                }
            }
            else
            {
                var documentFilesResult = await DocumentFilesAppService.GetListAsync(new GetDocumentFilesInput
                {
                    DocumentId = item.DocumentId,
                    MaxResultCount = 100,
                    SkipCount = 0
                });

                var pdfFile = documentFilesResult.Items
                    .FirstOrDefault(f => f.DocumentFile != null
                        && !string.IsNullOrEmpty(f.DocumentFile.Path)
                        && HC.Blazor.Shared.FileHelper.IsPdfFileExtension(f.DocumentFile.Name));

                pdfFilePath = pdfFile?.DocumentFile?.Path ?? string.Empty;

            }

            if (string.IsNullOrEmpty(pdfFilePath))
            {
                await UiMessageService.Warn(L["NoPdfAvailable"],
                    options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            // Get watermarked PDF from API (user + timestamp stamped)
            var fileBytes = await DocumentPdfViewerAppService.GetWatermarkedPdfAsync(new HC.DocumentPdfViewer.GetWatermarkedPdfInput
            {
                BlobPath = pdfFilePath,
                WatermarkAction = "view"
            });
            var base64 = Convert.ToBase64String(fileBytes);
            DocumentPdfFileUrl = $"data:application/pdf;base64,{base64}";
            IsDocumentPdfFile = true;

            await DocumentPdfViewerModal.Show();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error opening PDF viewer for document {DocumentId}", item.DocumentId);
            await UiMessageService.Warn(L["NoPdfAvailable"] + ": " + ex.Message,
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
        }
        finally
        {
            await BlockUiService.UnBlock();
        }
    }

    /// <summary>
    /// View signing document PDF from the Signing Documents tab
    /// </summary>
    private async Task ViewSigningDocumentPdfAsync(string filePath, string fileName)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            CurrentDocumentPdfDocumentId = null;
            var fileBytes = await DocumentPdfViewerAppService.GetWatermarkedPdfAsync(new HC.DocumentPdfViewer.GetWatermarkedPdfInput
            {
                BlobPath = filePath,
                WatermarkAction = "view"
            });
            var base64 = Convert.ToBase64String(fileBytes);
            DocumentPdfFileUrl = $"data:application/pdf;base64,{base64}";
            IsDocumentPdfFile = true;
            await DocumentPdfViewerModal.Show();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error viewing signing document PDF. FilePath: {FilePath}", filePath);
            await UiMessageService.Warn(L["NoPdfAvailable"] + ": " + ex.Message,
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
        }
        finally
        {
            await BlockUiService.UnBlock();
        }
    }

    private async Task CloseDocumentPdfViewerModalAsync()
    {
        if (DocumentPdfViewerModal != null)
        {
            await DocumentPdfViewerModal.Hide();
        }
        DocumentPdfFileUrl = null;
        IsDocumentPdfFile = false;
        CurrentDocumentPdfDocumentId = null;
    }

    private async Task AssignTaskFromDocumentPdfViewerAsync()
    {
        if (!CurrentDocumentPdfDocumentId.HasValue)
        {
            return;
        }

        var documentId = CurrentDocumentPdfDocumentId.Value;
        await CloseDocumentPdfViewerModalAsync();
        await CreateTaskModalRef.OpenCreateProjectTaskModalAsync(documentId);
    }

    private Task OnTaskCreatedFromPdfAsync()
    {
        return LoadDocumentSigningListAsync();
    }

    #endregion

    #region File Download

    private async Task DownloadFileAsync(string? filePath, string fileName)
    {
        if (string.IsNullOrEmpty(filePath))
            return;

        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            byte[] fileBytes;
            if (HC.Blazor.Shared.FileHelper.IsPdfFileExtension(fileName))
            {
                fileBytes = await DocumentPdfViewerAppService.GetWatermarkedPdfAsync(new HC.DocumentPdfViewer.GetWatermarkedPdfInput
                {
                    BlobPath = filePath,
                    WatermarkAction = "download"
                });
            }
            else
            {
                fileBytes = await BlobContainer.GetAllBytesAsync(filePath);
            }

            // Create blob URL and download using JavaScript
            var base64 = Convert.ToBase64String(fileBytes);
            var contentType = "application/octet-stream";
            var jsCode = $@"
                (function() {{
                    const blob = new Blob([Uint8Array.from(atob('{base64}'), c => c.charCodeAt(0))], {{ type: '{contentType}' }});
                    const url = window.URL.createObjectURL(blob);
                    const link = document.createElement('a');
                    link.href = url;
                    link.download = '{fileName}';
                    document.body.appendChild(link);
                    link.click();
                    document.body.removeChild(link);
                    window.URL.revokeObjectURL(url);
                }})();
            ";

            await JSRuntime.InvokeVoidAsync("eval", jsCode);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Error downloading file. FilePath: {filePath}, FileName: {fileName}");
            await HandleErrorAsync(ex);
        }
        finally
        {
            await BlockUiService.UnBlock();
        }
    }

    #endregion

    #region Dispose

    protected override void Dispose(bool disposing)
    {
        SearchDebounceCts?.Cancel();
        SearchDebounceCts?.Dispose();
        base.Dispose(disposing);
    }

    #endregion
}
