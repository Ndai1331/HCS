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
using HC.DocumentWorkflowInstances;
using HC.Permissions;
using HC.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Volo.Abp.AspNetCore.Components.Messages;

namespace HC.Blazor.Pages.Documents;

public partial class DocumentSigning
{
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

    // File upload in submit modal
    private FilePicker? WorkflowFilePicker { get; set; }
    private List<UploadedFileInfo> UploadedFiles { get; set; } = new();

    // Action Modal
    private Modal WorkflowActionModal { get; set; } = new();
    private DocumentSigningItemDto? SelectedDocumentForAction { get; set; }
    private string SelectedAction { get; set; } = "APPROVED";
    private string? ActionNote { get; set; }

    // Debounce
    private CancellationTokenSource? SearchDebounceCts { get; set; }

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
                DocumentId = SelectedDocumentId
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
                await UiMessageService.Error(L["PleaseSelectWorkflow"]);
                return;
            }

            // Validate document selection (required only when not using template file)
            if (!UseWorkflowTemplateFile && !SelectedDocumentId.HasValue)
            {
                await UiMessageService.Error(L["The {0} field is required.", L["Document"]]);
                return;
            }

            // Validate first step has users
            var firstStep = WorkflowSubmitInfo.Steps.OrderBy(s => s.Order).FirstOrDefault();
            if (firstStep == null || !firstStep.AssignedUsers.Any())
            {
                await UiMessageService.Error(L["FirstStepMustHaveAssignedUsers"]);
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
                AttachedFileIds = UploadedFiles.Any()
                    ? UploadedFiles.Select(f => f.DocumentFileId).ToList()
                    : null
            };

            await DocumentWorkflowInstancesAppService.SubmitToWorkflowAsync(input);

            await UiMessageService.Success(L["WorkflowSubmittedSuccessfully"]);
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

    private async Task ShowActionModalAsync(DocumentSigningItemDto document)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            SelectedDocumentForAction = document;
            SelectedAction = "APPROVED";
            ActionNote = null;
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
                await UiMessageService.Error(L["PleaseSelectAction"]);
                return;
            }

            if (!SelectedDocumentForAction.WorkflowInstanceId.HasValue || !SelectedDocumentForAction.MyAssignmentId.HasValue)
            {
                await UiMessageService.Error(L["NoActiveAssignment"]);
                return;
            }

            // Confirmation message based on action
            var confirmMessage = SelectedAction switch
            {
                "APPROVED" => L["ConfirmApprove"],
                "RETURNED" => L["ConfirmReturn"],
                "REJECTED" => L["ConfirmReject"],
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
                Note = ActionNote
            };

            await DocumentWorkflowInstancesAppService.ProcessWorkflowActionAsync(input);

            // Success message based on action
            var successMessage = SelectedAction switch
            {
                "APPROVED" => L["DocumentApprovedSuccessfully"],
                "RETURNED" => L["DocumentReturnedSuccessfully"],
                "REJECTED" => L["DocumentRejectedSuccessfully"],
                _ => L["ActionCompletedSuccessfully"]
            };

            await UiMessageService.Success(successMessage);
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
        "IN_PROGRESS" => "bg-info text-white",
        "COMPLETED" => "bg-success text-white",
        "REJECTED" => "bg-danger text-white",
        "RETURNED" => "bg-warning text-dark",
        "CANCELLED" => "bg-secondary text-white",
        "DRAFT" => "bg-light text-dark",
        _ => "bg-secondary text-white"
    };

    private string GetAssignmentStatusBadgeClass(string status) => status switch
    {
        "PENDING" => "bg-warning text-dark",
        "DONE" => "bg-success text-white",
        "REJECTED" => "bg-danger text-white",
        "REVOKED" => "bg-secondary text-white",
        _ => "bg-secondary text-white"
    };

    private Color GetActionButtonColor() => SelectedAction switch
    {
        "APPROVED" => Color.Success,
        "RETURNED" => Color.Warning,
        "REJECTED" => Color.Danger,
        _ => Color.Primary
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

    #region Dispose

    protected override void Dispose(bool disposing)
    {
        SearchDebounceCts?.Cancel();
        SearchDebounceCts?.Dispose();
        base.Dispose(disposing);
    }

    #endregion
}
