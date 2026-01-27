using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Blazorise;
using HC.ProjectTasks;
using HC.ProjectTaskAssignments;
using HC.ProjectTaskDocuments;
using HC.Permissions;
using HC.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Identity;
using HC.DocumentFiles;
using Volo.Abp.BlobStoring;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.JSInterop;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Components.Messages;
using Volo.Abp.AspNetCore.Components.Web.Theming.PageToolbars;

namespace HC.Blazor.Pages;

[Authorize(HCPermissions.ProjectTasks.Default)]
public partial class ProjectTasksDetail : HCComponentBase
{
    // Route parameter
    [Parameter] public Guid ProjectTaskId { get; set; }

    [SupplyParameterFromQuery(Name = "id")]
    public Guid? ProjectTaskIdQuery { get; set; }

    protected PageToolbar Toolbar { get; } = new PageToolbar();

    [Inject] private IProjectTasksAppService ProjectTasksAppService { get; set; } = default!;
    [Inject] private IProjectTaskAssignmentsAppService ProjectTaskAssignmentsAppService { get; set; } = default!;
    [Inject] private IProjectTaskDocumentsAppService ProjectTaskDocumentsAppService { get; set; } = default!;
    [Inject] private IDocumentFilesAppService DocumentFilesAppService { get; set; } = default!;
    [Inject] private IBlobContainer BlobContainer { get; set; } = default!;
    [Inject] private ILogger<ProjectTasksDetail> NotificationLogger { get; set; } = default!;
    [Inject] private IUiMessageService UiMessageService { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private IMemoryCache MemoryCache { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems { get; } = new();

    protected string PageTitle => CurrentProjectTask?.ProjectTask is null
        ? L["ProjectTasks"]
        : $"{CurrentProjectTask.ProjectTask.Code} - {CurrentProjectTask.ProjectTask.Title}";

    // Loading states
    protected bool IsLoadingProjectTask { get; set; }
    protected bool IsUpdatingProjectTask { get; set; }
    protected bool IsLoadingAssignments { get; set; }
    protected bool IsLoadingDocuments { get; set; }
    protected bool IsAddingAssignment { get; set; }
    protected bool IsAddingDocument { get; set; }



    private bool CanCreateProjectTask { get; set; }
    private bool CanEditProjectTask { get; set; }
    private bool CanDeleteProjectTask { get; set; }
    private bool CanCreateProjectTaskAssignment { get; set; }
    private bool CanEditProjectTaskAssignment { get; set; }
    private bool CanDeleteProjectTaskAssignment { get; set; }



    protected ProjectTaskWithNavigationPropertiesDto? CurrentProjectTask { get; set; }

    // Edit state (similar to modal)
    private string SelectedEditTab { get; set; } = "assignments";
    private ProjectTaskUpdateDto EditingProjectTask { get; set; } = new();
    private string? EditGeneralValidationErrorKey { get; set; }
    private Dictionary<string, string?> EditFieldErrors { get; set; } = new();

    // Select2 collections
    private IReadOnlyList<LookupDto<Guid>> ProjectsCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<LookupDto<Guid>> SelectedEditProjectTaskProject { get; set; } = new();

    protected sealed class ParentTaskSelectItem
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
    }

    private IReadOnlyList<ParentTaskSelectItem> ParentTasksCollection { get; set; } = new List<ParentTaskSelectItem>();
    private List<ParentTaskSelectItem> SelectedEditProjectTaskParentTask { get; set; } = new();

    private ProjectTaskPriority EditingProjectTaskPriority { get; set; } = ProjectTaskPriority.LOW;
    private ProjectTaskStatus EditingProjectTaskStatus { get; set; } = ProjectTaskStatus.TODO;

    private DatePicker<DateTime>? EditProjectTaskStartDateDatePicker { get; set; }
    private DatePicker<DateTime>? EditProjectTaskDueDateDatePicker { get; set; }

    // Assignments
    private IReadOnlyList<ProjectTaskAssignmentWithNavigationPropertiesDto> EditAssignmentsList { get; set; } = new List<ProjectTaskAssignmentWithNavigationPropertiesDto>();
    private IReadOnlyList<LookupDto<Guid>> AssignmentIdentityUsersCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<LookupDto<Guid>> EditAssignmentsUsersToAdd { get; set; } = new();
    private ProjectTaskAssignmentRole EditAssignmentRole { get; set; } = ProjectTaskAssignmentRole.MAIN;
    private string? EditAssignmentNote { get; set; }

    // Documents
    private IReadOnlyList<ProjectTaskDocumentWithNavigationPropertiesDto> EditDocumentsList { get; set; } = new List<ProjectTaskDocumentWithNavigationPropertiesDto>();
    private IReadOnlyList<LookupDto<Guid>> DocumentsLookupCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<LookupDto<Guid>> EditDocumentsToAdd { get; set; } = new();
    private ProjectTaskDocumentPurpose EditDocumentPurpose { get; set; } = ProjectTaskDocumentPurpose.REPORT;

    // PDF viewer
    private Modal? PdfViewerModal { get; set; }
    private bool IsPdfFile { get; set; }
    private string? PdfFileUrl { get; set; }
    private Dictionary<Guid, bool> DocumentHasPdfCache { get; set; } = new();

    // Helper methods
    private string? GetEditFieldError(string fieldName) => EditFieldErrors.GetValueOrDefault(fieldName);
    private bool HasEditFieldError(string fieldName) => EditFieldErrors.ContainsKey(fieldName) && !string.IsNullOrWhiteSpace(EditFieldErrors[fieldName]);

    protected virtual ValueTask SetBreadcrumbItemsAsync()
    {
        BreadcrumbItems.Clear();
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["ProjectTasks"], "/project-tasks"));
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["Details"]));
        return ValueTask.CompletedTask;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await LoadProjectTaskAsync();
            await SetPermissionsAsync();
            await SetToolbarItemsAsync();
            await InvokeAsync(StateHasChanged);
        }
    }   
    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["Back"], () =>
        {
            NavigationManager.NavigateTo("/tasks");
            return Task.CompletedTask;
        }, IconName.ArrowLeft);
        if (CurrentProjectTask != null && CanEditProjectTask)
        {
            Toolbar.AddButton(L["Edit"], UpdateProjectTaskAsync, IconName.Edit, Color.Primary);
        }
        if (CurrentProjectTask != null && CanDeleteProjectTask)
        {
            Toolbar.AddButton(L["Delete"], DeleteProjectTaskAsync, IconName.Delete, Color.Danger);
        }
        return ValueTask.CompletedTask;
    }
    protected virtual async Task SetPermissionsAsync()
    {
        CanEditProjectTask = await AuthorizationService.IsGrantedAsync(HCPermissions.ProjectTasks.Edit);
        CanDeleteProjectTask = await AuthorizationService.IsGrantedAsync(HCPermissions.ProjectTasks.Delete);
        CanCreateProjectTask = await AuthorizationService.IsGrantedAsync(HCPermissions.ProjectTasks.Create);
        CanCreateProjectTaskAssignment = await AuthorizationService.IsGrantedAsync(HCPermissions.ProjectTaskAssignments.Create);
        CanEditProjectTaskAssignment = await AuthorizationService.IsGrantedAsync(HCPermissions.ProjectTaskAssignments.Edit);
        CanDeleteProjectTaskAssignment = await AuthorizationService.IsGrantedAsync(HCPermissions.ProjectTaskAssignments.Delete);
    }

    private async Task LoadProjectTaskAsync()
    {
        IsLoadingProjectTask = true;
        try
        {
            await InvokeAsync(StateHasChanged);

            CurrentProjectTask = await ProjectTasksAppService.GetWithNavigationPropertiesAsync(ProjectTaskId);
            
            if (CurrentProjectTask != null)
            {
                EditingProjectTask = ObjectMapper.Map<ProjectTaskDto, ProjectTaskUpdateDto>(CurrentProjectTask.ProjectTask);
                EditGeneralValidationErrorKey = null;
                EditFieldErrors.Clear();

                // Initialize Select2 selections
                SelectedEditProjectTaskProject = new List<LookupDto<Guid>>();
                if (EditingProjectTask.ProjectId != Guid.Empty)
                {
                    var displayName = CurrentProjectTask.Project?.Name ?? string.Empty;
                    var selectedProject = new LookupDto<Guid>
                    {
                        Id = EditingProjectTask.ProjectId,
                        DisplayName = string.IsNullOrWhiteSpace(displayName) ? EditingProjectTask.ProjectId.ToString() : displayName
                    };
                    SelectedEditProjectTaskProject.Add(selectedProject);
                    ProjectsCollection = ProjectsCollection.Concat(new[] { selectedProject }).DistinctBy(x => x.Id).ToList();
                }

                SelectedEditProjectTaskParentTask = new List<ParentTaskSelectItem>();
                if (!string.IsNullOrWhiteSpace(EditingProjectTask.ParentTaskId))
                {
                    var parent = new ParentTaskSelectItem
                    {
                        Id = EditingProjectTask.ParentTaskId!,
                        DisplayName = EditingProjectTask.ParentTaskId!
                    };
                    SelectedEditProjectTaskParentTask.Add(parent);
                    ParentTasksCollection = ParentTasksCollection.Concat(new[] { parent }).DistinctBy(x => x.Id).ToList();
                }

                // Initialize enum selects
                if (Enum.TryParse<ProjectTaskPriority>(EditingProjectTask.Priority, ignoreCase: true, out var priority))
                {
                    EditingProjectTaskPriority = priority;
                }
                if (Enum.TryParse<ProjectTaskStatus>(EditingProjectTask.Status, ignoreCase: true, out var status))
                {
                    EditingProjectTaskStatus = status;
                }

                // Load tabs data
                EditAssignmentsUsersToAdd = new List<LookupDto<Guid>>();
                EditAssignmentRole = ProjectTaskAssignmentRole.MAIN;
                EditAssignmentNote = null;
                EditDocumentsToAdd = new List<LookupDto<Guid>>();
                EditDocumentPurpose = ProjectTaskDocumentPurpose.REPORT;

                await LoadEditAssignmentsAsync();
                await LoadEditDocumentsAsync();
                
                // Preload lookup collections
                await GetProjectCollectionLookupAsync(new List<LookupDto<Guid>>(), "", CancellationToken.None);
                await GetAssignmentIdentityUserCollectionLookupAsync();
                await GetDocumentCollectionLookupAsync();
                
                // Cache PDF info for documents
                await CacheDocumentPdfInfoAsync(EditDocumentsList);
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsLoadingProjectTask = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task LoadEditAssignmentsAsync()
    {
        IsLoadingAssignments = true;
        try
        {
            await InvokeAsync(StateHasChanged);
            var input = new GetProjectTaskAssignmentsInput { ProjectTaskId = ProjectTaskId };
            var result = await ProjectTaskAssignmentsAppService.GetListAsync(input);
            EditAssignmentsList = result.Items;
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsLoadingAssignments = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task LoadEditDocumentsAsync()
    {
        IsLoadingDocuments = true;
        try
        {
            await InvokeAsync(StateHasChanged);
            var input = new GetProjectTaskDocumentsInput { ProjectTaskId = ProjectTaskId };
            var result = await ProjectTaskDocumentsAppService.GetListAsync(input);
            EditDocumentsList = result.Items;
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsLoadingDocuments = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task UpdateProjectTaskAsync()
    {
        if (IsUpdatingProjectTask)
        {
            return;
        }

        IsUpdatingProjectTask = true;
        try
        {
            await InvokeAsync(StateHasChanged);

            if (!ValidateEditGeneralInformation())
            {
                await UiMessageService.Warn(L[EditGeneralValidationErrorKey ?? "ValidationError"]);
                await InvokeAsync(StateHasChanged);
                return;
            }

            var updated = await ProjectTasksAppService.UpdateAsync(ProjectTaskId, EditingProjectTask);
            
            // Reload data
            await LoadProjectTaskAsync();
            await UiMessageService.Success(L["SuccessfullyUpdated"]);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsUpdatingProjectTask = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private bool ValidateEditGeneralInformation()
    {
        EditGeneralValidationErrorKey = null;
        EditFieldErrors.Clear();

        bool isValid = true;

        if (EditingProjectTask.ProjectId == Guid.Empty)
        {
            EditFieldErrors["Project"] = L["ProjectRequired"];
            EditGeneralValidationErrorKey = "ProjectRequired";
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(EditingProjectTask.Code))
        {
            EditFieldErrors["Code"] = L["CodeRequired"];
            if (isValid)
            {
                EditGeneralValidationErrorKey = "CodeRequired";
            }
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(EditingProjectTask.Title))
        {
            EditFieldErrors["Title"] = L["TitleRequired"];
            if (isValid)
            {
                EditGeneralValidationErrorKey = "TitleRequired";
            }
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(EditingProjectTask.Priority))
        {
            EditFieldErrors["Priority"] = L["PriorityRequired"];
            if (isValid)
            {
                EditGeneralValidationErrorKey = "PriorityRequired";
            }
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(EditingProjectTask.Status))
        {
            EditFieldErrors["Status"] = L["StatusRequired"];
            if (isValid)
            {
                EditGeneralValidationErrorKey = "StatusRequired";
            }
            isValid = false;
        }

        if (EditingProjectTask.ProgressPercent < ProjectTaskConsts.ProgressPercentMinLength
            || EditingProjectTask.ProgressPercent > ProjectTaskConsts.ProgressPercentMaxLength)
        {
            EditFieldErrors["ProgressPercent"] = L["ProgressPercentRange"];
            if (isValid)
            {
                EditGeneralValidationErrorKey = "ProgressPercentRange";
            }
            isValid = false;
        }

        return isValid;
    }

    private void OnSelectedEditTabChanged(string name)
    {
        SelectedEditTab = name;
        InvokeAsync(StateHasChanged);
    }

    protected async Task<List<LookupDto<Guid>>> GetProjectCollectionLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        ProjectsCollection = (await ProjectTasksAppService.GetProjectLookupAsync(new LookupRequestDto { Filter = filter })).Items;
        return ProjectsCollection.ToList();
    }

    private void OnEditProjectTaskProjectChanged()
    {
        if (SelectedEditProjectTaskProject.Count > 0)
        {
            EditingProjectTask.ProjectId = SelectedEditProjectTaskProject[0].Id;
        }
        else
        {
            EditingProjectTask.ProjectId = Guid.Empty;
        }
    }

    protected async Task<List<ParentTaskSelectItem>> GetParentTaskCollectionLookupAsync(IReadOnlyList<ParentTaskSelectItem> dbset, string filter, CancellationToken token)
    {
        var input = new GetProjectTasksInput
        {
            FilterText = filter,
            MaxResultCount = LimitedResultRequestDto.DefaultMaxResultCount
        };
        var result = await ProjectTasksAppService.GetListAsync(input);
        
        var items = result.Items
            .Where(x => x.ProjectTask.Id != ProjectTaskId) // Exclude current task
            .Select(x => new ParentTaskSelectItem
            {
                Id = x.ProjectTask.Id.ToString(),
                DisplayName = $"{x.ProjectTask.Code} - {x.ProjectTask.Title}"
            })
            .ToList();
        
        ParentTasksCollection = items;
        return items;
    }

    private void OnEditProjectTaskParentChanged()
    {
        if (SelectedEditProjectTaskParentTask.Count > 0 && Guid.TryParse(SelectedEditProjectTaskParentTask[0].Id, out var parentId))
        {
            EditingProjectTask.ParentTaskId = parentId.ToString();
        }
        else
        {
            EditingProjectTask.ParentTaskId = null;
        }
    }

    // Priority/Status changes
    private void OnEditingProjectTaskPriorityChanged(ProjectTaskPriority value)
    {
        EditingProjectTaskPriority = value;
        EditingProjectTask.Priority = value.ToString();
        EditFieldErrors.Remove("Priority");
    }

    private void OnEditingProjectTaskStatusChanged(ProjectTaskStatus value)
    {
        EditingProjectTaskStatus = value;
        EditingProjectTask.Status = value.ToString();
        EditFieldErrors.Remove("Status");
    }

    // Assignments
    protected async Task<List<LookupDto<Guid>>> GetAssignmentIdentityUserLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        AssignmentIdentityUsersCollection = (await ProjectTaskAssignmentsAppService.GetIdentityUserLookupAsync(new LookupRequestDto { Filter = filter })).Items;
        return AssignmentIdentityUsersCollection.ToList();
    }

    protected void OnEditAssignmentUserChanged()
    {
        // Select2 (single-select) uses list; force rerender so Add button enables.
        InvokeAsync(StateHasChanged);
    }

    private async Task AddEditAssignmentAsync()
    {
        if (IsAddingAssignment)
        {
            return;
        }

        if (EditAssignmentsUsersToAdd.Count == 0)
        {
            await UiMessageService.Warn(L["PleaseSelectAssignee"]);
            return;
        }

        IsAddingAssignment = true;
        try
        {
            await InvokeAsync(StateHasChanged);

            var userId = EditAssignmentsUsersToAdd[0].Id;
            var input = new ProjectTaskAssignmentCreateDto
            {
                ProjectTaskId = ProjectTaskId,
                UserId = userId,
                AssignmentRole = EditAssignmentRole.ToString(),
                AssignedAt = DateTime.Now,
                Note = EditAssignmentNote
            };

            await ProjectTaskAssignmentsAppService.CreateAsync(input);
            
            EditAssignmentsUsersToAdd = new List<LookupDto<Guid>>();
            EditAssignmentNote = null;
            await LoadEditAssignmentsAsync();
            await UiMessageService.Success(L["SuccessfullyAdded"]);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsAddingAssignment = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task DeleteEditAssignmentAsync(ProjectTaskAssignmentWithNavigationPropertiesDto row)
    {
        try
        {
            if (!await UiMessageService.Confirm(L["DeleteConfirmationMessage"]))
            {
                return;
            }

            await ProjectTaskAssignmentsAppService.DeleteAsync(row.ProjectTaskAssignment.Id);
            await LoadEditAssignmentsAsync();
            await UiMessageService.Success(L["SuccessfullyDeleted"]);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    // Documents
    protected async Task<List<LookupDto<Guid>>> GetDocumentLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var result = await ProjectTaskDocumentsAppService.GetDocumentLookupAsync(new LookupRequestDto
        {
            Filter = filter,
            MaxResultCount = 20,
            SkipCount = 0
        });

        DocumentsLookupCollection = result.Items;
        return result.Items.ToList();
    }

    protected void OnEditDocumentChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private async Task AddEditDocumentAsync()
    {
        if (IsAddingDocument)
        {
            return;
        }

        if (EditDocumentsToAdd.Count == 0)
        {
            await UiMessageService.Warn(L["PleaseSelectDocument"]);
            return;
        }

        IsAddingDocument = true;
        try
        {
            await InvokeAsync(StateHasChanged);

            var documentId = EditDocumentsToAdd[0].Id;
            await ProjectTaskDocumentsAppService.CreateAsync(new ProjectTaskDocumentCreateDto
            {
                ProjectTaskId = ProjectTaskId,
                DocumentId = documentId,
                DocumentPurpose = EditDocumentPurpose.ToString()
            });

            EditDocumentsToAdd = new List<LookupDto<Guid>>();
            await LoadEditDocumentsAsync();
            
            // Cache PDF info
            if (documentId != Guid.Empty)
            {
                var hasPdf = await CheckIfDocumentHasPdfFileAsync(documentId);
                DocumentHasPdfCache[documentId] = hasPdf;
            }
            
            await UiMessageService.Success(L["SuccessfullyAdded"]);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsAddingDocument = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task DeleteEditDocumentAsync(ProjectTaskDocumentWithNavigationPropertiesDto row)
    {
        try
        {
            if (!await UiMessageService.Confirm(L["DeleteConfirmationMessage"]))
            {
                return;
            }

            await ProjectTaskDocumentsAppService.DeleteAsync(row.ProjectTaskDocument.Id);
            
            if (row.Document?.Id != null)
            {
                DocumentHasPdfCache.Remove(row.Document.Id);
            }
            
            await LoadEditDocumentsAsync();
            await UiMessageService.Success(L["SuccessfullyDeleted"]);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task DeleteProjectTaskAsync()
    {
        try
        {
            if (!await UiMessageService.Confirm(L["DeleteConfirmationMessage"]))
            {
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            await ProjectTasksAppService.DeleteAsync(ProjectTaskId);
            await UiMessageService.Success(L["SuccessfullyDeleted"]);
            NavigationManager.NavigateTo("/tasks");
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            await BlockUiService.UnBlock();
            await InvokeAsync(StateHasChanged);
        }
    }

    // PDF viewer
    private async Task OpenPdfViewerModalForDocumentAsync(ProjectTaskDocumentWithNavigationPropertiesDto context)
    {
        if (context.Document == null)
        {
            return;
        }

        try
        {
            var documentId = context.Document.Id;
            var hasPdf = DocumentHasPdfFile(documentId);
            
            if (!hasPdf)
            {
                await UiMessageService.Warn(L["NoPdfFileAvailable"]);
                return;
            }

            // Get PDF file URL
            var pdfFile = await DocumentFilesAppService.GetListAsync(new GetDocumentFilesInput
            {
                DocumentId = documentId,
                MaxResultCount = 1
            });

            if (pdfFile.Items.Any())
            {
                var file = pdfFile.Items.First();
                if (!string.IsNullOrEmpty(file.DocumentFile.Path))
                {
                    var fileBytes = await BlobContainer.GetAllBytesAsync(file.DocumentFile.Path);
                    var base64 = Convert.ToBase64String(fileBytes);
                    PdfFileUrl = $"data:application/pdf;base64,{base64}";
                    IsPdfFile = true;
                    if (PdfViewerModal != null)
                    {
                        await PdfViewerModal.Show();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task ClosePdfViewerModalAsync()
    {
        if (PdfViewerModal != null)
        {
            await PdfViewerModal.Hide();
        }
        PdfFileUrl = null;
        IsPdfFile = false;
    }

    private async Task DownloadDocumentFileAsync(ProjectTaskDocumentWithNavigationPropertiesDto context)
    {
        if (context.Document == null)
        {
            return;
        }

        try
        {
            var documentId = context.Document.Id;
            var files = await DocumentFilesAppService.GetListAsync(new GetDocumentFilesInput
            {
                DocumentId = documentId,
                MaxResultCount = 1
            });

            if (files.Items.Any())
            {
                var file = files.Items.First();
                if (!string.IsNullOrEmpty(file.DocumentFile.Path))
                {
                    var fileBytes = await BlobContainer.GetAllBytesAsync(file.DocumentFile.Path);
                    var base64 = Convert.ToBase64String(fileBytes);
                    var contentType = "application/octet-stream";
                    var jsCode = $@"
                        (function() {{
                            const blob = new Blob([Uint8Array.from(atob('{base64}'), c => c.charCodeAt(0))], {{ type: '{contentType}' }});
                            const url = window.URL.createObjectURL(blob);
                            const link = document.createElement('a');
                            link.href = url;
                            link.download = '{file.DocumentFile.Name ?? "download"}';
                            document.body.appendChild(link);
                            link.click();
                            document.body.removeChild(link);
                            window.URL.revokeObjectURL(url);
                        }})();
                    ";
                    await JSRuntime.InvokeVoidAsync("eval", jsCode);
                }
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private bool DocumentHasPdfFile(Guid documentId)
    {
        if (DocumentHasPdfCache.TryGetValue(documentId, out var cached))
        {
            return cached;
        }
        return false;
    }

    private async Task<bool> CheckIfDocumentHasPdfFileAsync(Guid documentId)
    {
        try
        {
            var files = await DocumentFilesAppService.GetListAsync(new GetDocumentFilesInput
            {
                DocumentId = documentId,
                MaxResultCount = 100
            });

            return files.Items.Any(f => !string.IsNullOrEmpty(f.DocumentFile.Name) && 
                f.DocumentFile.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private async Task GetAssignmentIdentityUserCollectionLookupAsync()
    {
        var input = new LookupRequestDto { MaxResultCount = LimitedResultRequestDto.DefaultMaxResultCount };
        var result = await ProjectTaskAssignmentsAppService.GetIdentityUserLookupAsync(input);
        AssignmentIdentityUsersCollection = result.Items;
    }

    private async Task GetDocumentCollectionLookupAsync()
    {
        var result = await ProjectTaskDocumentsAppService.GetDocumentLookupAsync(new LookupRequestDto
        {
            MaxResultCount = 20,
            SkipCount = 0
        });
        DocumentsLookupCollection = result.Items;
    }

    private async Task CacheDocumentPdfInfoAsync(IReadOnlyList<ProjectTaskDocumentWithNavigationPropertiesDto> documents)
    {
        foreach (var doc in documents)
        {
            if (doc.Document?.Id != null && !DocumentHasPdfCache.ContainsKey(doc.Document.Id))
            {
                var hasPdf = await CheckIfDocumentHasPdfFileAsync(doc.Document.Id);
                DocumentHasPdfCache[doc.Document.Id] = hasPdf;
            }
        }
    }

}
