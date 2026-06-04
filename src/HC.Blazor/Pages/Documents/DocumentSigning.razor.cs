using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
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
using HC.Blazor.Shared;
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
    private IReadOnlyList<DateTime?> SelectedDateRange { get; set; } = new List<DateTime?>();
    private DateTime? FromDate { get; set; }
    private DateTime? ToDate { get; set; }
    private string? FilterText { get; set; }
    private DocumentSigningFilterMode CurrentFilterMode { get; set; } = DocumentSigningFilterMode.All;
    private DocumentSigningDateFilterField ExportDateFilterField { get; set; } = DocumentSigningDateFilterField.IncomingDate;
    private bool IsExporting { get; set; }

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
    private bool IsSigningBlocked { get; set; }
    private bool CanExtendWorkflow { get; set; }
    private DateTime? WorkflowGraceCancelAt { get; set; }
    private int WorkflowExtensionCount { get; set; }
    private int WorkflowTotalExtensionBusinessDays { get; set; }
    private bool AllowReturnAction { get; set; }

    private Modal ExtendWorkflowModal { get; set; } = new();
    private int ExtensionBusinessDaysInput { get; set; } = 1;
    private string? ExtensionReasonInput { get; set; }
    private bool IsExtendingWorkflow { get; set; }

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
    private WorkflowStepDetailDto? NextStepDetailForApprove { get; set; }
    private Guid? SelectedNextStepSignerUserId { get; set; }
    private DocumentWorkflowInstanceDto? WorkflowInstanceInfo { get; set; }

    // All workflow steps with their signing status (for action modal step overview)
    private List<WorkflowStepStatusDto> AllStepsWithStatus { get; set; } = new();
    private bool _scrollToCurrentWorkflowStepPending;

    private Dictionary<Guid, Guid?> EditedStepSigners { get; set; } = new();
    private bool IsSavingWorkflowSigners { get; set; }

    private bool CanEditWorkflowSigners =>
        AllStepsWithStatus.Any(s => s.CanEditSigner);

    private bool HasWorkflowSignerChanges =>
        AllStepsWithStatus
            .Where(s => s.CanEditSigner)
            .Any(s => EditedStepSigners.TryGetValue(s.StepId, out var selected)
                && selected.HasValue
                && selected != s.CurrentPendingReceiverUserId);

    /// <summary>Aligned with REMOTE_CA default API timeout (~30s) for predictable UX.</summary>
    private const int WorkflowActionSigningUiTimeoutSeconds = 30;

    private bool IsActionModalLoading { get; set; }

    /// <summary>Shows RadarSpinner overlay on the workflow action modal body while busy.</summary>
    private bool IsWorkflowActionSubmitting { get; set; }

    private bool IsActionModalBusy =>
        IsActionModalLoading || IsSavingWorkflowSigners || IsExtendingWorkflow || IsWorkflowActionSubmitting;

    private int WorkflowActionCountdownRemaining { get; set; } = WorkflowActionSigningUiTimeoutSeconds;

    private bool IsResubmitModalLoading { get; set; }

    private bool IsResubmitModalBusy => IsResubmitModalLoading;

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
            var today = Clock.Now.Date;
            var from = today.AddDays(-60);
            SelectedDateRange = new List<DateTime?> { from, today };
            SyncFilterDatesFromRange();

            BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["DocumentSigning"]));
            await SetToolbarItemsAsync();
            await LoadDocumentSigningListAsync();
            IsInitialDataLoaded = true;
            await TryAutoOpenActionModalFromNotificationAsync();
            await RequestRenderAsync();
        }

        if (_scrollToCurrentWorkflowStepPending)
        {
            _scrollToCurrentWorkflowStepPending = false;
            await ScrollToCurrentWorkflowStepAsync();
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
        Toolbar.AddButton(L["ExportToExcel"], async () =>
        {
            await DownloadAsExcelAsync();
        }, IconName.Download, requiredPolicyName: HCPermissions.Documents.SubmitForSigning);

        Toolbar.AddButton(L["CreateSigning"], async () =>
        {
            await ShowSubmitWorkflowModalAsync();
        }, IconName.Add, requiredPolicyName: HCPermissions.Documents.SubmitForSigning);

        return Task.CompletedTask;
    }

    private Task OnExportDateFilterFieldChanged(DocumentSigningDateFilterField value)
    {
        ExportDateFilterField = value;
        return Task.CompletedTask;
    }

    private async Task DownloadAsExcelAsync()
    {
        if (IsExporting)
        {
            await UiMessageService.Info(L["Exporting"], options: options => options.OkButtonText = L["Ok"]);
            return;
        }

        IsExporting = true;
        await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
        try
        {
            await UiMessageService.Info(L["Exporting"], options: options => options.OkButtonText = L["Ok"]);
            SyncFilterDatesFromRange();
            var token = (await DocumentWorkflowInstancesAppService.GetDownloadTokenAsync()).Token;
            var remoteService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("HC")
                ?? await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
            var culture = CultureInfo.CurrentUICulture.Name ?? CultureInfo.CurrentCulture.Name;
            if (!culture.IsNullOrEmpty())
            {
                culture = "&culture=" + culture;
            }

            var url = $"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/document-workflow-instances/document-signing-as-excel-file" +
                      $"?DownloadToken={HttpUtility.UrlEncode(token)}{culture}" +
                      $"&FilterText={HttpUtility.UrlEncode(FilterText)}" +
                      $"&FilterMode={(int)CurrentFilterMode}" +
                      $"&DateFilterField={(int)ExportDateFilterField}" +
                      $"&FromDate={FromDate?.ToString("O")}" +
                      $"&ToDate={ToDate?.ToString("O")}";

            NavigationManager.NavigateTo(url, forceLoad: true);
        }
        finally
        {
            await BlockUiService.UnBlock();
            IsExporting = false;
        }
    }

    #endregion

    #region Data Loading

    private async Task LoadDocumentSigningListAsync()
    {
        SyncFilterDatesFromRange();
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
            InitializeEditedStepSigners();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading all steps with status for workflow instance {InstanceId}", workflowInstanceId);
            AllStepsWithStatus = new();
            EditedStepSigners = new();
            await HandleErrorAsync(ex);
        }
    }

    private void InitializeEditedStepSigners()
    {
        EditedStepSigners = AllStepsWithStatus
            .Where(s => s.CanEditSigner && s.CurrentPendingReceiverUserId.HasValue)
            .ToDictionary(s => s.StepId, s => s.CurrentPendingReceiverUserId);
    }

    private void OnEditedStepSignerChanged(Guid stepId, Guid? userId)
    {
        EditedStepSigners[stepId] = userId;
        UpdateSigningActionVisibility();
    }

    /// <summary>
    /// Effective pending signer for a step (unsaved edit takes precedence).
    /// </summary>
    private static bool IsWorkflowStepProcessed(WorkflowStepStatusDto step, IReadOnlyList<WorkflowStepStatusDto> allSteps)
    {
        if (step.IsCompleted)
        {
            return true;
        }

        var current = allSteps.FirstOrDefault(s => s.IsCurrentStep);
        return current != null && step.Order < current.Order;
    }

    private string GetWorkflowStepCardClass(WorkflowStepStatusDto step)
    {
        if (step.IsCurrentStep)
        {
            return "workflow-step-card workflow-step-card-current";
        }

        if (IsWorkflowStepProcessed(step, AllStepsWithStatus))
        {
            return "workflow-step-card workflow-step-card-processed";
        }

        return "workflow-step-card";
    }

    private void RequestScrollToCurrentWorkflowStep()
    {
        if (AllStepsWithStatus.Any(s => s.IsCurrentStep))
        {
            _scrollToCurrentWorkflowStepPending = true;
        }
    }

    private async Task ScrollToCurrentWorkflowStepAsync()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync(
                "eval",
                """
                (() => {
                  const step = document.getElementById('workflow-step-current');
                  const container = document.getElementById('workflow-steps-scroll-container');
                  if (!step || !container) return;
                  const stepTop = step.offsetTop - container.offsetTop;
                  const stepBottom = stepTop + step.offsetHeight;
                  const viewTop = container.scrollTop;
                  const viewBottom = viewTop + container.clientHeight;
                  if (stepTop < viewTop || stepBottom > viewBottom) {
                    step.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
                  }
                })()
                """);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Scroll to current workflow step failed");
        }
    }

    private Guid? GetEffectivePendingSignerUserId(WorkflowStepStatusDto step)
    {
        if (EditedStepSigners.TryGetValue(step.StepId, out var edited) && edited.HasValue)
        {
            return edited;
        }

        return step.CurrentPendingReceiverUserId;
    }

    /// <summary>
    /// True when the current user still has the active PENDING assignment on the current workflow step.
    /// </summary>
    private bool CanUserProcessCurrentWorkflowStep()
    {
        if (SelectedDocumentForAction == null
            || !SelectedDocumentForAction.MyAssignmentId.HasValue
            || !string.Equals(
                SelectedDocumentForAction.MyAssignmentStatus,
                nameof(DocumentAssignmentStatus.PENDING),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var currentStep = AllStepsWithStatus.FirstOrDefault(s => s.IsCurrentStep);
        if (currentStep == null)
        {
            return SelectedDocumentForAction.CanAct;
        }

        var effectiveSignerId = GetEffectivePendingSignerUserId(currentStep);
        if (!effectiveSignerId.HasValue)
        {
            return SelectedDocumentForAction.CanAct;
        }

        return CurrentUser.Id.HasValue && effectiveSignerId.Value == CurrentUser.Id.Value;
    }

    /// <summary>
    /// Signing actions are shown only when the current user is the active pending signer on the current step.
    /// Opening via "view" still allows signing when the user has a pending assignment (e.g. after reassignment).
    /// </summary>
    private void UpdateSigningActionVisibility()
    {
        IsViewOnly = !CanUserProcessCurrentWorkflowStep();
    }

    private async Task RefreshSelectedDocumentActionStateAsync()
    {
        if (SelectedDocumentForAction == null)
        {
            return;
        }

        var updated = await FetchSigningItemForNotificationAsync(SelectedDocumentForAction.DocumentId);
        if (updated != null)
        {
            SelectedDocumentForAction = updated;
        }

        UpdateSigningActionVisibility();
    }

    private async Task SaveWorkflowStepSignersAsync()
    {
        if (SelectedDocumentForAction?.WorkflowInstanceId == null || !HasWorkflowSignerChanges)
        {
            return;
        }

        var selections = AllStepsWithStatus
            .Where(s => s.CanEditSigner
                && EditedStepSigners.TryGetValue(s.StepId, out var selected)
                && selected.HasValue
                && selected != s.CurrentPendingReceiverUserId)
            .Select(s => new WorkflowStepSignerSelectionDto
            {
                StepId = s.StepId,
                SelectedUserId = EditedStepSigners[s.StepId]!.Value
            })
            .ToList();

        if (!selections.Any())
        {
            return;
        }

        try
        {
            IsSavingWorkflowSigners = true;

            await DocumentWorkflowInstancesAppService.UpdateWorkflowStepSignersAsync(
                new UpdateWorkflowStepSignersInput
                {
                    WorkflowInstanceId = SelectedDocumentForAction.WorkflowInstanceId.Value,
                    StepSignerSelections = selections
                });

            await UiMessageService.Success(L["WorkflowSignersUpdatedSuccessfully"]);
            await RefreshActionModalDataAsync();
            await SearchAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsSavingWorkflowSigners = false;
            await RequestRenderAsync();
        }
    }

    private static string GetCandidateDisplayLabel(WorkflowStepUserDto user)
    {
        var name = string.IsNullOrWhiteSpace(user.FullName) ? user.UserName : user.FullName;
        if (!string.IsNullOrWhiteSpace(user.OrganizationUnitName))
        {
            return $"{name} — {user.OrganizationUnitName}";
        }

        return name ?? string.Empty;
    }

    #endregion

    #region Filter Events

    private Task OnSelectedDateRangeChanged(IReadOnlyList<DateTime?> dates)
    {
        SelectedDateRange = dates;
        return Task.CompletedTask;
    }

    private async Task OnFilterSearchAsync()
    {
        SyncFilterDatesFromRange();
        await SearchAsync();
    }

    private void SyncFilterDatesFromRange()
    {
        if (SelectedDateRange != null && SelectedDateRange.Count >= 2)
        {
            FromDate = SelectedDateRange[0];
            ToDate = SelectedDateRange[1];
            return;
        }

        var today = Clock.Now.Date;
        FromDate = today.AddDays(-60);
        ToDate = today;
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
        await RequestRenderAsync();
    }

    private async Task SearchAsync()
    {
        CurrentPage = 1;
        await LoadDocumentSigningListAsync();
        await RequestRenderAsync();
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
        await RequestRenderAsync();
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
        await RequestRenderAsync();
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

            await ShowActionModalAsync(targetDocument, viewOnly: !targetDocument.CanAct);
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

    private async Task ApplyOverdueCheckAsync(Guid workflowInstanceId)
    {
        try
        {
            var overdueResult = await DocumentWorkflowInstancesAppService.CheckAndHandleOverdueAsync(workflowInstanceId);
            IsOverdue = overdueResult.IsOverdue;
            CanExtendWorkflow = overdueResult.CanExtend;
            WorkflowGraceCancelAt = overdueResult.GraceCancelAt;
            WorkflowExtensionCount = overdueResult.ExtensionCount;
            WorkflowTotalExtensionBusinessDays = overdueResult.TotalExtensionBusinessDays;
            AllowReturnAction = overdueResult.AllowReturn;
            IsSigningBlocked = overdueResult.WorkflowStatus == nameof(DocumentWorkflowInstanceStatus.CANCELLED)
                || (overdueResult.WorkflowStatus == nameof(DocumentWorkflowInstanceStatus.OVERDUE)
                    && overdueResult.GraceCancelAt.HasValue
                    && Clock.Now >= overdueResult.GraceCancelAt.Value);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "ApplyOverdueCheckAsync failed for workflow instance {InstanceId}", workflowInstanceId);
            IsSigningBlocked = true;
            await HandleErrorAsync(ex);
        }
    }

    private async Task RefreshActionModalDataAsync()
    {
        if (SelectedDocumentForAction?.WorkflowInstanceId == null)
        {
            return;
        }

        var workflowInstanceId = SelectedDocumentForAction.WorkflowInstanceId.Value;
        var documentId = SelectedDocumentForAction.DocumentId;

        try
        {
            var bundle = await DocumentWorkflowInstancesAppService.GetActionBundleAsync(
                new GetWorkflowInstanceActionBundleInput
                {
                    WorkflowInstanceId = workflowInstanceId,
                    DocumentId = documentId,
                    SigningMethodsMaxResultCount = 100
                });

            WorkflowInstanceInfo = bundle.Instance;
            CurrentStepDetailInfo = bundle.CurrentStepDetail;
            NextStepDetailForApprove = bundle.NextStepDetail;
            SelectedNextStepSignerUserId = null;
            WorkflowLogs = bundle.Logs ?? new();
            WorkflowFiles = bundle.Files ?? new();
            DocumentHistories = bundle.DocumentHistories ?? new();
            AllStepsWithStatus = bundle.AllStepsWithStatus ?? new();
            InitializeEditedStepSigners();

            if (WorkflowInstanceInfo != null)
            {
                SelectedDocumentForAction.WorkflowStatus = WorkflowInstanceInfo.Status;
                SelectedDocumentForAction.WorkflowFinishedAt = WorkflowInstanceInfo.FinishedAt > DateTime.MinValue
                    ? WorkflowInstanceInfo.FinishedAt
                    : null;
                SelectedDocumentForAction.WorkflowOverdueAt = WorkflowInstanceInfo.OverdueAt;
                SelectedDocumentForAction.ExtensionCount = WorkflowInstanceInfo.ExtensionCount;
                SelectedDocumentForAction.TotalExtensionBusinessDays = WorkflowInstanceInfo.TotalExtensionBusinessDays;
                SelectedDocumentForAction.WorkflowGraceCancelAt = WorkflowInstanceInfo.OverdueAt.HasValue
                    ? HC.Workflows.BusinessDayCalculator.GetOverdueGraceCancelAt(WorkflowInstanceInfo.OverdueAt.Value)
                    : null;
            }

            if (!IsViewOnly)
            {
                await ApplyOverdueCheckAsync(workflowInstanceId);
            }

            UpdateSigningActionVisibility();
            RequestScrollToCurrentWorkflowStep();
            await RequestRenderAsync();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "RefreshActionModalDataAsync failed for instance {InstanceId}", workflowInstanceId);
            await HandleErrorAsync(ex);
        }
    }

    private async Task ShowActionModalAsync(DocumentSigningItemDto document, bool viewOnly = false)
    {
        var loadSucceeded = false;
        _showActionModalRichTextEditors = false;
        SelectedDocumentForAction = document;
            SelectedAction = nameof(WorkflowInstanceLogAction.APPROVE);
            ActionNote = null;
            ActionModalActiveTab = "general";
            WorkflowLogs = new();
            WorkflowFiles = new();
            DocumentHistories = new();
            SigningDocumentAssignments = new();
            CurrentStepDetailInfo = null;
            NextStepDetailForApprove = null;
            SelectedNextStepSignerUserId = null;
            WorkflowInstanceInfo = null;
            AllStepsWithStatus = new();
            EditedStepSigners = new();
            IsSavingWorkflowSigners = false;
            IsOverdue = false;
            IsSigningBlocked = false;
            CanExtendWorkflow = false;
            WorkflowGraceCancelAt = null;
            WorkflowExtensionCount = 0;
            WorkflowTotalExtensionBusinessDays = 0;
            AllowReturnAction = false;
            IsViewOnly = viewOnly;
            SelectedSigningMethodId = null;
            AvailableUserSignaturesForMethod = new();
            SelectedUserSignatureId = null;
            IsActionModalLoading = true;

            _actionModalEditorSessionKey++;
            await InvokeAsync(WorkflowActionModal.Show);
            await RequestRenderAsync();

            try
            {
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
                    NextStepDetailForApprove = bundle.NextStepDetail;
                    SelectedNextStepSignerUserId = null;
                    WorkflowLogs = bundle.Logs ?? new();
                    WorkflowFiles = bundle.Files ?? new();
                    DocumentHistories = bundle.DocumentHistories ?? new();
                    AllStepsWithStatus = bundle.AllStepsWithStatus ?? new();
                    InitializeEditedStepSigners();
                    SigningMethods = bundle.SigningMethods ?? new();

                    await LoadSigningDocumentFilesAsync(document.DocumentId);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Signing action-bundle failed; falling back to per-call loads");
                    await HandleErrorAsync(ex);
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
                    InitializeEditedStepSigners();
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

            if (document.WorkflowInstanceId.HasValue && !IsViewOnly)
            {
                await ApplyOverdueCheckAsync(document.WorkflowInstanceId.Value);
            }

            UpdateSigningActionVisibility();
            loadSucceeded = true;
            }
            catch (Exception ex)
            {
                SelectedDocumentForAction = null;
                _showActionModalRichTextEditors = false;
                await HandleErrorAsync(ex);
                await InvokeAsync(WorkflowActionModal.Hide);
            }
            finally
            {
                IsActionModalLoading = false;
                if (loadSucceeded)
                {
                    await Task.Delay(100);
                    _showActionModalRichTextEditors = true;
                    RequestScrollToCurrentWorkflowStep();
                    await RequestRenderAsync();
                }
            }
    }

    private async Task HideWorkflowActionModalAsync()
    {
        _showActionModalRichTextEditors = false;
        await RequestRenderAsync();
        await Task.Delay(50);
        await InvokeAsync(WorkflowActionModal.Hide);
    }

    private async Task CloseActionModalAsync()
    {
        await HideWorkflowActionModalAsync();
    }

    private async Task RunWorkflowActionCountdownAsync(CancellationToken cancellationToken)
    {
        WorkflowActionCountdownRemaining = WorkflowActionSigningUiTimeoutSeconds;
        await RequestRenderAsync().ConfigureAwait(false);
        try
        {
            while (WorkflowActionCountdownRemaining > 0
                   && IsWorkflowActionSubmitting
                   && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                if (!IsWorkflowActionSubmitting || cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                WorkflowActionCountdownRemaining--;
                await RequestRenderAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Countdown cancelled when the API completes or the modal resets.
        }
    }

    private async Task ConfirmWorkflowActionAsync()
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
        if (!confirmed)
        {
            return;
        }

        using var countdownCts = new CancellationTokenSource();
        var countdownTask = Task.CompletedTask;

        try
        {
            IsWorkflowActionSubmitting = true;
            WorkflowActionCountdownRemaining = WorkflowActionSigningUiTimeoutSeconds;
            countdownTask = RunWorkflowActionCountdownAsync(countdownCts.Token);
            await RequestRenderAsync();

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
                UserSignatureId = SelectedUserSignatureId,
                NextStepSignerUserId = null
            };

            var apiTask = DocumentWorkflowInstancesAppService.ProcessWorkflowActionAsync(input);
            var delayTask = Task.Delay(TimeSpan.FromSeconds(WorkflowActionSigningUiTimeoutSeconds));
            await Task.WhenAny(apiTask, delayTask);

            if (!apiTask.IsCompleted)
            {
                await UiMessageService.Error(L["RemoteSigningConnectionFailed"],
                    options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            await apiTask;

            var successMessage = SelectedAction switch
            {
                nameof(WorkflowInstanceLogAction.APPROVE) => L["DocumentApprovedSuccessfully"],
                nameof(WorkflowInstanceLogAction.RETURN) => L["DocumentReturnedSuccessfully"],
                nameof(WorkflowInstanceLogAction.REJECT) => L["DocumentRejectedSuccessfully"],
                _ => L["ActionCompletedSuccessfully"]
            };

            await UiMessageService.Success(successMessage,
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            await HideWorkflowActionModalAsync();
            await LoadDocumentSigningListAsync();
            await RequestRenderAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            countdownCts.Cancel();
            try
            {
                await countdownTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Ignore torn-down countdown.
            }

            IsWorkflowActionSubmitting = false;
            WorkflowActionCountdownRemaining = WorkflowActionSigningUiTimeoutSeconds;
            await RequestRenderAsync();
        }
    }

    #endregion

    #region Cancel Workflow

    private async Task ConfirmCancelWorkflowAsync(DocumentSigningItemDto document)
    {
        if (!document.WorkflowInstanceId.HasValue)
        {
            return;
        }

        var confirmed = await UiMessageService.Confirm(L["ConfirmCancelWorkflow"]);
        if (!confirmed)
        {
            return;
        }

        try
        {
            await DocumentWorkflowInstancesAppService.CancelWorkflowByInitiatorAsync(
                new CancelWorkflowByInitiatorInput
                {
                    WorkflowInstanceId = document.WorkflowInstanceId.Value
                });
            await UiMessageService.Success(L["WorkflowCancelledSuccessfully"]);
            await LoadDocumentSigningListAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
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

        IsResubmitModalLoading = true;
        _resubmitModalEditorSessionKey++;
        await InvokeAsync(ResubmitWorkflowModal.Show);
        await RequestRenderAsync();

        try
        {
            await LoadMyDocumentsAsync();

            if (document.WorkflowInstanceId.HasValue)
            {
                ReturnedWorkflowInfo = await DocumentWorkflowInstancesAppService
                    .GetReturnedWorkflowInfoAsync(document.WorkflowInstanceId.Value);

                if (ReturnedWorkflowInfo != null)
                {
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
            await InvokeAsync(ResubmitWorkflowModal.Hide);
        }
        finally
        {
            IsResubmitModalLoading = false;
            if (loadSucceeded)
            {
                await Task.Delay(100);
                _showResubmitModalRichTextEditors = true;
                await RequestRenderAsync();
            }
        }
    }

    private async Task HideResubmitWorkflowModalAsync()
    {
        _showResubmitModalRichTextEditors = false;
        await RequestRenderAsync();
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
            await RequestRenderAsync();
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

            IsResubmitModalLoading = true;
            await RequestRenderAsync();

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
                DeleteFileIds = ResubmitDeleteFileIds.Any() ? ResubmitDeleteFileIds : null,
                ViewStepScopeSelections = BuildResubmitViewStepScopeSelections()
            };

            await DocumentWorkflowInstancesAppService.ResubmitReturnedWorkflowAsync(input);

            await UiMessageService.Success(L["WorkflowResubmittedSuccessfully"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            await HideResubmitWorkflowModalAsync();
            await LoadDocumentSigningListAsync();
            await RequestRenderAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsResubmitModalLoading = false;
            await RequestRenderAsync();
        }
    }

    private List<WorkflowStepViewScopeSelectionDto> BuildResubmitViewStepScopeSelections()
    {
        if (ReturnedWorkflowInfo?.WorkflowInfo?.Steps == null)
        {
            return new List<WorkflowStepViewScopeSelectionDto>();
        }

        return ReturnedWorkflowInfo.WorkflowInfo.Steps
            .Where(s => s.IsViewStep)
            .Select(s => new WorkflowStepViewScopeSelectionDto
            {
                StepId = s.StepId,
                OrganizationUnitIds = s.TemplateOrganizationUnitIds.ToList(),
                UserIds = s.TemplateUserIds.ToList()
            })
            .Where(s => s.OrganizationUnitIds.Any() || s.UserIds.Any())
            .ToList();
    }

    #endregion

    #region Helper Methods

    private string GetFilterItemClass(DocumentSigningFilterMode mode)
    {
        return CurrentFilterMode == mode
            ? "list-group-item-action active cursor-pointer"
            : "list-group-item-action cursor-pointer";
    }

    private string? GetWorkflowExpiryCountdownText(DocumentSigningItemDto item)
    {
        if (string.Equals(item.WorkflowStatus, nameof(DocumentWorkflowInstanceStatus.OVERDUE), StringComparison.OrdinalIgnoreCase)
            && item.WorkflowGraceCancelAt.HasValue)
        {
            var graceRemaining = item.WorkflowGraceCancelAt.Value - Clock.Now;
            if (graceRemaining <= TimeSpan.Zero)
            {
                return L["WorkflowOverdueGraceExpired"];
            }

            return L["WorkflowGracePeriodRemaining", graceRemaining.Days, graceRemaining.Hours, graceRemaining.Minutes];
        }

        if (!string.Equals(item.WorkflowStatus, nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS), StringComparison.OrdinalIgnoreCase)
            && !string.Equals(item.WorkflowStatus, nameof(DocumentWorkflowInstanceStatus.OVERDUE), StringComparison.OrdinalIgnoreCase)
            || !item.WorkflowFinishedAt.HasValue
            || item.WorkflowFinishedAt.Value <= DateTime.MinValue)
        {
            return null;
        }

        var remaining = item.WorkflowFinishedAt.Value - Clock.Now;
        if (remaining <= TimeSpan.Zero)
        {
            return L["WorkflowSigningExpired"];
        }

        return L["WorkflowTimeBeforeExpiry", remaining.Days, remaining.Hours, remaining.Minutes];
    }

    private string GetWorkflowStatusBadgeClass(string status) => status switch
    {
        nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS) => "bg-info text-white",
        nameof(DocumentWorkflowInstanceStatus.OVERDUE) => "bg-danger text-white",
        nameof(DocumentWorkflowInstanceStatus.COMPLETED) => "bg-success text-white",
        nameof(DocumentWorkflowInstanceStatus.REJECTED) => "bg-danger text-white",
        nameof(DocumentWorkflowInstanceStatus.RETURNED) => "bg-warning text-dark",
        nameof(DocumentWorkflowInstanceStatus.CANCELLED) => "bg-secondary text-white",
        nameof(DocumentWorkflowInstanceStatus.DRAFT) => "bg-light text-dark",
        _ => "bg-secondary text-white"
    };

    private string GetWorkflowExpiryTextClass(DocumentSigningItemDto item)
    {
        if (string.Equals(item.WorkflowStatus, nameof(DocumentWorkflowInstanceStatus.OVERDUE), StringComparison.OrdinalIgnoreCase))
        {
            return "text-danger fw-semibold";
        }

        if (!item.WorkflowFinishedAt.HasValue)
        {
            return "text-muted";
        }

        if (item.WorkflowFinishedAt.Value <= Clock.Now)
        {
            return "text-danger fw-semibold";
        }

        if (item.CanAct && item.WorkflowFinishedAt.Value <= Clock.Now.AddDays(1))
        {
            return "text-danger";
        }

        if (item.CanAct)
        {
            return "text-warning fw-semibold";
        }

        return "text-muted";
    }

    private async Task ShowExtendWorkflowModalAsync()
    {
        ExtensionBusinessDaysInput = 1;
        ExtensionReasonInput = null;
        await ExtendWorkflowModal.Show();
    }

    private async Task CloseExtendWorkflowModalAsync()
    {
        await ExtendWorkflowModal.Hide();
    }

    private async Task ConfirmExtendWorkflowAsync()
    {
        if (SelectedDocumentForAction?.WorkflowInstanceId == null)
        {
            return;
        }

        if (ExtensionBusinessDaysInput < 1)
        {
            await UiMessageService.Warn(L["ExtensionBusinessDaysMustBePositive"]);
            return;
        }

        if (string.IsNullOrWhiteSpace(ExtensionReasonInput))
        {
            await UiMessageService.Warn(L["ExtensionReasonRequired"]);
            return;
        }

        try
        {
            IsExtendingWorkflow = true;

            await DocumentWorkflowInstancesAppService.ExtendWorkflowAsync(new ExtendWorkflowInput
            {
                WorkflowInstanceId = SelectedDocumentForAction.WorkflowInstanceId.Value,
                ExtensionBusinessDays = ExtensionBusinessDaysInput,
                Reason = ExtensionReasonInput.Trim()
            });

            await UiMessageService.Success(L["WorkflowExtendedSuccessfully"]);
            await CloseExtendWorkflowModalAsync();
            await RefreshActionModalDataAsync();
            await SearchAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsExtendingWorkflow = false;
            await RequestRenderAsync();
        }
    }

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

    private static bool IsPlainTextWorkflowLog(string? action) =>
        string.Equals(action, nameof(WorkflowInstanceLogAction.EXTEND_WORKFLOW), StringComparison.OrdinalIgnoreCase)
        || string.Equals(action, nameof(WorkflowInstanceLogAction.UPDATE_SIGNER), StringComparison.OrdinalIgnoreCase);

    private string GetLogActionBadgeClass(string action) => action switch
    {
        nameof(WorkflowInstanceLogAction.SUBMIT_WORKFLOW) => "bg-primary text-white",
        nameof(WorkflowInstanceLogAction.APPROVE) => "bg-success text-white",
        nameof(WorkflowInstanceLogAction.RETURN) => "bg-warning text-dark",
        nameof(WorkflowInstanceLogAction.REJECT) => "bg-danger text-white",
        nameof(WorkflowInstanceLogAction.SIGN) => "bg-success text-white",
        nameof(WorkflowInstanceLogAction.UPDATE_SIGNER) => "bg-info text-white",
        nameof(WorkflowInstanceLogAction.EXTEND_WORKFLOW) => "bg-info text-white",
        _ => "bg-secondary text-white"
    };

    private string GetLogActionIcon(string action) => action switch
    {
        nameof(WorkflowInstanceLogAction.SUBMIT_WORKFLOW) => "bi bi-play-circle-fill",
        nameof(WorkflowInstanceLogAction.APPROVE) => "bi bi-check-circle-fill",
        nameof(WorkflowInstanceLogAction.RETURN) => "bi bi-arrow-return-left",
        nameof(WorkflowInstanceLogAction.REJECT) => "bi bi-x-circle-fill",
        nameof(WorkflowInstanceLogAction.SIGN) => "bi bi-pen-fill",
        nameof(WorkflowInstanceLogAction.UPDATE_SIGNER) => "bi bi-person-gear",
        nameof(WorkflowInstanceLogAction.EXTEND_WORKFLOW) => "bi bi-calendar-plus",
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
    /// Open PDF viewer for a workflow document using the latest signed PDF (not the submit copy).
    /// </summary>
    private async Task OpenDocumentPdfViewerModalAsync(DocumentSigningItemDto item)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            CurrentDocumentPdfDocumentId = item.DocumentId;

            var pdfFileUrl = await WorkflowPdfDisplayHelper.LoadPdfDataUrlAsync(
                item.DocumentId,
                DocumentWorkflowInstancesAppService,
                DocumentPdfViewerAppService);

            if (string.IsNullOrEmpty(pdfFileUrl))
            {
                await UiMessageService.Warn(L["NoPdfAvailable"],
                    options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            DocumentPdfFileUrl = pdfFileUrl;
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
    /// View signing document PDF from the Signing Documents tab (enables Assign Task for the workflow document).
    /// </summary>
    private async Task ViewSigningDocumentPdfAsync(Guid documentId, string filePath, string fileName)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            CurrentDocumentPdfDocumentId = documentId;
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
