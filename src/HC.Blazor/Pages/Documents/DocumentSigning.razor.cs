using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Blazorise;
using Blazorise.DataGrid;
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

namespace HC.Blazor.Pages.Documents;

public partial class DocumentSigning
{
    [Inject] private IDocumentAssignmentsAppService DocumentAssignmentsAppService { get; set; } = default!;
    [Inject] private IMasterDatasAppService MasterDatasAppService { get; set; } = default!;

    #region Properties

    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new();
    protected PageToolbar Toolbar { get; } = new PageToolbar();

    // Filter properties
    private DateTime? FromDate { get; set; } = DateTime.Now.AddDays(-60);
    private DateTime? ToDate { get; set; } = DateTime.Now;
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

    // Submit Workflow Modal
    private Modal SubmitWorkflowModal { get; set; } = new();
    private Guid? SelectedWorkflowId { get; set; }
    private IReadOnlyList<LookupDto<Guid>> AvailableWorkflows { get; set; } = new List<LookupDto<Guid>>();
    private WorkflowSubmitInfoDto? WorkflowSubmitInfo { get; set; }
    private bool UseTemplateFile { get; set; } = true;

    /// <summary>
    /// If true, use the workflow template file to create a new Document + DocumentFile.
    /// Available when the selected workflow template has a file path.
    /// </summary>
    private bool UseWorkflowTemplateFile { get; set; }

    // Document selection in submit modal
    private List<DocumentWithNavigationPropertiesDto> MyDocumentsList { get; set; } = new();
    private Guid? SelectedDocumentId { get; set; }
    private DocumentWithNavigationPropertiesDto? SelectedDocumentDto { get; set; }
    private int ModalResetKey { get; set; } // Used to force re-render Autocomplete via @key

    // Signing content
    private string? SigningContent { get; set; }

    // File upload in submit modal
    private FilePicker? WorkflowFilePicker { get; set; }
    private List<UploadedFileInfo> UploadedFiles { get; set; } = new();

    // Action Modal
    private Modal WorkflowActionModal { get; set; } = new();
    private DocumentSigningItemDto? SelectedDocumentForAction { get; set; }
    private string SelectedAction { get; set; } = nameof(WorkflowInstanceLogAction.APPROVE);
    private string? ActionNote { get; set; }

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

    // Signing Document Files (from DocumentAssignments.DocumentFileResultId)
    private List<DocumentAssignmentWithNavigationPropertiesDto> SigningDocumentAssignments { get; set; } = new();
    private bool IsLoadingSigningDocuments { get; set; }

    // Current Step Detail (loaded from WorkflowStepTemplates + WorkflowStepAssignments)
    private WorkflowStepDetailDto? CurrentStepDetailInfo { get; set; }
    private DocumentWorkflowInstanceDto? WorkflowInstanceInfo { get; set; }

    // Debounce
    private CancellationTokenSource? SearchDebounceCts { get; set; }

    // PDF Viewer Modal
    private Modal DocumentPdfViewerModal { get; set; } = new();
    private string? DocumentPdfFileUrl { get; set; }
    private bool IsDocumentPdfFile { get; set; }

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
            BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["DocumentSigning"]));
            await SetToolbarItemsAsync();
            await LoadWorkflowLookupAsync();
            await LoadDocumentSigningListAsync();
            await InvokeAsync(StateHasChanged);
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

    private async Task LoadWorkflowLookupAsync()
    {
        try
        {
            var result = await DocumentWorkflowInstancesAppService.GetWorkflowLookupAsync(
                new LookupRequestDto { MaxResultCount = 1000 });
            AvailableWorkflows = result.Items;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading workflow lookup");
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
                MaxResultCount = 1000,
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
            // Load workflow instance to get StartedAt, FinishedAt, CurrentStepId, WorkflowId
            WorkflowInstanceInfo = await DocumentWorkflowInstancesAppService.GetAsync(workflowInstanceId);

            if (WorkflowInstanceInfo != null)
            {
                // Load workflow submit info to get all steps with assigned users
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

    #region Submit Workflow Modal

    private async Task ShowSubmitWorkflowModalAsync()
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);

            // Reset modal state
            SelectedDocumentId = null;
            SelectedDocumentDto = null;
            SelectedWorkflowId = null;
            WorkflowSubmitInfo = null;
            UseTemplateFile = true;
            UseWorkflowTemplateFile = false;
            SigningContent = null;
            UploadedFiles.Clear();
            ModalResetKey++; // Force re-render Autocomplete to clear text

            // Clear FilePicker
            if (WorkflowFilePicker != null)
            {
                await WorkflowFilePicker.Clear();
            }

            // Load personal documents for selection
            await LoadMyDocumentsAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            await BlockUiService.UnBlock();
            await InvokeAsync(SubmitWorkflowModal.Show);
        }
    }

    private void OnDocumentSelected(Guid? documentId)
    {
        SelectedDocumentId = documentId;
        SelectedDocumentDto = documentId.HasValue
            ? MyDocumentsList.FirstOrDefault(d => d.Document.Id == documentId.Value)
            : null;
    }

    private void OnUseWorkflowTemplateFileChanged(bool value)
    {
        UseWorkflowTemplateFile = value;
        if (value)
        {
            // When using template file, clear document selection
            SelectedDocumentId = null;
            SelectedDocumentDto = null;
        }
    }

    private async Task OnWorkflowSelectedAsync(Guid? workflowId)
    {
        SelectedWorkflowId = workflowId;
        WorkflowSubmitInfo = null;

        if (workflowId.HasValue && workflowId.Value != Guid.Empty)
        {
            try
            {
                await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
                WorkflowSubmitInfo = await DocumentWorkflowInstancesAppService.GetWorkflowSubmitInfoAsync(workflowId.Value);
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

        await InvokeAsync(StateHasChanged);
    }

    private async Task CloseSubmitWorkflowModalAsync()
    {
        await InvokeAsync(SubmitWorkflowModal.Hide);
    }

    /// <summary>
    /// Blazorise Upload event handler - fires once per file when UploadAll() is triggered.
    /// Blazorise handles sequential file reading internally, avoiding Blazor Server
    /// RemoteJSDataStream pipe issues. This is the official Blazorise pattern.
    /// See: https://blazorise.com/docs/components/file-picker
    /// </summary>
    private async Task OnFileUpload(FileUploadEventArgs e)
    {
        try
        {
            using var memoryStream = new MemoryStream();
            await e.File.OpenReadStream(long.MaxValue).CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            // Generate unique file path
            var extension = Path.GetExtension(e.File.Name);
            var filePath = $"workflow-files/{Guid.NewGuid()}{extension}";

            // Upload to blob storage
            using var uploadStream = new MemoryStream(fileBytes);
            await BlobContainer.SaveAsync(filePath, uploadStream);

            // Create DocumentFile record
            var documentFileDto = await DocumentFilesAppService.CreateAsync(new DocumentFileCreateDto
            {
                Name = e.File.Name,
                Path = filePath,
                Hash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(fileBytes)),
                IsSigned = false,
                UploadedAt = DateTime.Now,
                // DocumentId = SelectedDocumentId
            });

            UploadedFiles.Add(new UploadedFileInfo
            {
                DocumentFileId = documentFileDto.Id,
                Name = e.File.Name,
                Path = filePath
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error uploading file {FileName}", e.File.Name);
            await HandleErrorAsync(ex);
        }
        finally
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ConfirmSubmitWorkflowAsync()
    {
        try
        {
            // Validate workflow selection
            if (!SelectedWorkflowId.HasValue || WorkflowSubmitInfo == null)
            {
                await UiMessageService.Error(L["PleaseSelectWorkflow"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            // Validate document selection (required only when not using template file)
            if (!UseWorkflowTemplateFile && !SelectedDocumentId.HasValue)
            {
                await UiMessageService.Error(L["The {0} field is required.", L["Document"]],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            // Validate first step has users
            var firstStep = WorkflowSubmitInfo.Steps.OrderBy(s => s.Order).FirstOrDefault();
            if (firstStep == null || !firstStep.AssignedUsers.Any())
            {
                await UiMessageService.Error(L["FirstStepMustHaveAssignedUsers"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            var confirmed = await UiMessageService.Confirm(L["ConfirmSubmitForSigning"]);
            if (!confirmed) return;

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);

            var input = new SubmitToWorkflowInput
            {
                DocumentId = UseWorkflowTemplateFile ? null : SelectedDocumentId,
                WorkflowId = SelectedWorkflowId.Value,
                UseWorkflowTemplateFile = UseWorkflowTemplateFile,
                UseTemplateFile = UseTemplateFile,
                SigningContent = SigningContent,
                AttachedFileIds = UploadedFiles.Any()
                    ? UploadedFiles.Select(f => f.DocumentFileId).ToList()
                    : null
            };

            await DocumentWorkflowInstancesAppService.SubmitToWorkflowAsync(input);

            await UiMessageService.Success(L["WorkflowSubmittedSuccessfully"],
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            await InvokeAsync(SubmitWorkflowModal.Hide);
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

    #region Workflow Action Modal

    private async Task ShowActionModalAsync(DocumentSigningItemDto document, bool viewOnly = false)
    {
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
            IsOverdue = false;
            AllowReturnAction = false;
            IsViewOnly = viewOnly;
            SelectedSigningMethodId = null;

            // Load all data in parallel
            var tasks = new List<Task>();

            if (document.WorkflowInstanceId.HasValue)
            {
                tasks.Add(LoadWorkflowLogsAsync(document.WorkflowInstanceId.Value));
                tasks.Add(LoadWorkflowFilesAsync(document.WorkflowInstanceId.Value));
                tasks.Add(LoadCurrentStepDetailAsync(document.WorkflowInstanceId.Value));
            }

            tasks.Add(LoadDocumentHistoriesAsync(document.DocumentId));
            tasks.Add(LoadSigningMethodsAsync());
            tasks.Add(LoadSigningDocumentFilesAsync(document.DocumentId));

            await Task.WhenAll(tasks);

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
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            await BlockUiService.UnBlock();
            await InvokeAsync(WorkflowActionModal.Show);
        }
    }

    private async Task CloseActionModalAsync()
    {
        await InvokeAsync(WorkflowActionModal.Hide);
    }

    private async Task ConfirmWorkflowActionAsync()
    {
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

            var input = new WorkflowActionInput
            {
                DocumentWorkflowInstanceId = SelectedDocumentForAction.WorkflowInstanceId.Value,
                DocumentAssignmentId = SelectedDocumentForAction.MyAssignmentId.Value,
                Action = SelectedAction,
                Note = ActionNote,
                SigningMethodId = SelectedSigningMethodId
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

            await UiMessageService.Success(successMessage,
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            await InvokeAsync(WorkflowActionModal.Hide);
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

            string? pdfFilePath = null;

            // Step 1: Try to get the original document files (same as DocumentDetail.LoadPdfUrlAsync)
            var documentFilesResult = await DocumentFilesAppService.GetListAsync(new GetDocumentFilesInput
            {
                DocumentId = item.DocumentId,
                MaxResultCount = 100,
                SkipCount = 0
            });

            // Find the first PDF file from the document's files
            var pdfFile = documentFilesResult.Items
                .FirstOrDefault(f => f.DocumentFile != null
                    && !string.IsNullOrEmpty(f.DocumentFile.Path)
                    && HC.Blazor.Shared.FileHelper.IsPdfFileExtension(f.DocumentFile.Name));

            if (pdfFile != null)
            {
                pdfFilePath = pdfFile.DocumentFile.Path;
            }
            else
            {
                // Step 2: Fallback - check DocumentAssignment's DocumentFileResultId (signed result file)
                var assignmentsResult = await DocumentAssignmentsAppService.GetListAsync(new GetDocumentAssignmentsInput
                {
                    DocumentId = item.DocumentId,
                    MaxResultCount = 100,
                    SkipCount = 0
                });

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

            if (string.IsNullOrEmpty(pdfFilePath))
            {
                await UiMessageService.Warn(L["NoPdfAvailable"],
                    options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            // Get file bytes from blob storage and create data URL
            var fileBytes = await BlobContainer.GetAllBytesAsync(pdfFilePath);
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
            var fileBytes = await BlobContainer.GetAllBytesAsync(filePath);
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
            var fileBytes = await BlobContainer.GetAllBytesAsync(filePath);

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
