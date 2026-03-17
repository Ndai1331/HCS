using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HC.ProjectTaskAssignments;
using HC.ProjectTaskDocuments;
using HC.ProjectTasks;
using HC.Shared;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.AspNetCore.Components.Messages;
namespace HC.Blazor.Pages;

public partial class ProjectTasks
{
    private async Task OpenCreateProjectTaskModalAsync()
    {
        // Use the ProjectTaskCreateModal component instead of inline modal
        if (ProjectTaskCreateModalRef != null)
        {
            await ProjectTaskCreateModalRef.OpenCreateProjectTaskModalAsync();
        }
    }
    
    /// <summary>
    /// Callback handler when a new task is created via the ProjectTaskCreateModal component
    /// </summary>
    private async Task OnProjectTaskCreatedAsync()
    {
        // Reload kanban to reflect new task
        await RefreshKanbanAsync();
        await GetProjectTasksAsync();
        await InvokeAsync(StateHasChanged);
    }
    
    // Generate next available ProjectTask code (Txxxxxx format)
    private async Task<string> GenerateNextProjectTaskCodeAsync()
    {
        try
        {
            int maxNumber = 0;
            const int pageSize = 1000; // Process in batches
            int skipCount = 0;
            bool hasMore = true;
            
            // Query all project tasks in batches to find the highest "T" code
            while (hasMore)
            {
                var input = new GetProjectTasksInput
                {
                    MaxResultCount = pageSize,
                    SkipCount = skipCount,
                    Sorting = "ProjectTask.Code DESC" // Sort by code descending
                };
                
                var result = await ProjectTasksAppService.GetListAsync(input);
                
                if (result.Items == null || result.Items.Count == 0)
                {
                    hasMore = false;
                    break;
                }
                
                // Iterate through items to find the highest "T" code
                foreach (var task in result.Items)
                {
                    if (!string.IsNullOrWhiteSpace(task.ProjectTask.Code))
                    {
                        var code = task.ProjectTask.Code.Trim();
                        
                        // Check if code starts with "T" (case-insensitive) and has numeric suffix
                        if (code.StartsWith("T", StringComparison.OrdinalIgnoreCase) && code.Length > 1)
                        {
                            // Extract number part after "T"
                            var numberPart = code.Substring(1);
                            if (int.TryParse(numberPart, out int number))
                            {
                                if (number > maxNumber)
                                {
                                    maxNumber = number;
                                }
                            }
                        }
                    }
                }
                
                // Check if there are more items to process
                if (result.Items.Count < pageSize || skipCount + pageSize >= result.TotalCount)
                {
                    hasMore = false;
                }
                else
                {
                    skipCount += pageSize;
                }
            }
            
            // Generate next code: T + (maxNumber + 1) with 6 digits padding
            return $"T{(maxNumber + 1):D7}";
        }
        catch (Exception ex)
        {
            // Fallback to T000001 if error occurs
            Logger?.LogError(ex, "Error generating project task code");
            return "T000001";
        }
    }

    private async Task CloseCreateProjectTaskModalAsync()
    {
        NewProjectTask = new ProjectTaskDto
        {
            StartDate = DateTime.Now,
            DueDate = DateTime.Now,
            Priority = ProjectTaskPriority.LOW.ToString(),
            Status = ProjectTaskStatus.TODO.ToString(),
        };
        CreateWizardProjectTaskId = Guid.Empty;
        CreateGeneralValidationErrorKey = null;
        CreateFieldErrors.Clear();
        CreateAssignmentsUsersToAdd = new List<LookupDto<Guid>>();
        CreateAssignmentsList = new List<ProjectTaskAssignmentWithNavigationPropertiesDto>();
        CreateDocumentsToAdd = new List<LookupDto<Guid>>();
        CreateDocumentsList = new List<ProjectTaskDocumentWithNavigationPropertiesDto>();
        SelectedCreateTab = "general";
        // Note: CreateProjectTaskModal has been moved to ProjectTaskCreateModal component
        // The component manages its own modal state
        await Task.CompletedTask;
    }

    private async Task CancelCreateWizardAsync()
    {
        try
        {
            if (CreateWizardProjectTaskId != Guid.Empty)
            {
                if (!await UiMessageService.Confirm(L["CreateWizard:CancelAndDeleteTask"].Value))
                {
                    return;
                }

                // Best-effort cleanup to avoid leaving a task without assignments.
                await ProjectTasksAppService.DeleteAsync(CreateWizardProjectTaskId);
            }

            await CloseCreateProjectTaskModalAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task SaveGeneralInformationAsync()
    {
        if (IsSavingGeneralInformation || IsCreateWizardGeneralSaved)
        {
            return;
        }

        IsSavingGeneralInformation = true;
        try
        {
            await InvokeAsync(StateHasChanged);

            if (!ValidateCreateGeneralInformation())
            {
                await UiMessageService.Warn(L[CreateGeneralValidationErrorKey ?? "ValidationError"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                await InvokeAsync(StateHasChanged);
                return;
            }

            var input = new ProjectTaskCreateDto
            {
                ParentTaskId = NewProjectTask.ParentTaskId,
                Code = NewProjectTask.Code,
                Title = NewProjectTask.Title,
                Description = NewProjectTask.Description,
                StartDate = NewProjectTask.StartDate,
                DueDate = NewProjectTask.DueDate,
                Priority = NewProjectTaskPriority.ToString(),
                Status = NewProjectTaskStatus.ToString(),
                ProgressPercent = NewProjectTask.ProgressPercent,
                ProjectId = NewProjectTask.ProjectId
            };

            var created = await ProjectTasksAppService.CreateAsync(input);
            CreateWizardProjectTaskId = created.Id;

            // Load step-2 data after task is created.
            await LoadCreateAssignmentsAsync();
            await LoadCreateDocumentsAsync();

            SelectedCreateTab = "assignments";
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsSavingGeneralInformation = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private bool ValidateCreateGeneralInformation()
    {
        // Reset error state.
        CreateGeneralValidationErrorKey = null;
        CreateFieldErrors.Clear();

        bool isValid = true;

        // Required: Project
        if (NewProjectTask.ProjectId == Guid.Empty)
        {
            CreateFieldErrors["Project"] = L["ProjectRequired"];
            CreateGeneralValidationErrorKey = "ProjectRequired";
            isValid = false;
        }

        // Required: Code
        if (string.IsNullOrWhiteSpace(NewProjectTask.Code))
        {
            CreateFieldErrors["Code"] = L["CodeRequired"];
            if (isValid)
            {
                CreateGeneralValidationErrorKey = "CodeRequired";
            }
            isValid = false;
        }

        // Required: Title
        if (string.IsNullOrWhiteSpace(NewProjectTask.Title))
        {
            CreateFieldErrors["Title"] = L["TitleRequired"];
            if (isValid)
            {
                CreateGeneralValidationErrorKey = "TitleRequired";
            }
            isValid = false;
        }

        // Required: Priority/Status are strings on DTO (enum-backed selects fill them).
        if (string.IsNullOrWhiteSpace(NewProjectTask.Priority))
        {
            CreateFieldErrors["Priority"] = L["PriorityRequired"];
            if (isValid)
            {
                CreateGeneralValidationErrorKey = "PriorityRequired";
            }
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(NewProjectTask.Status))
        {
            CreateFieldErrors["Status"] = L["StatusRequired"];
            if (isValid)
            {
                CreateGeneralValidationErrorKey = "StatusRequired";
            }
            isValid = false;
        }

        // Range: ProgressPercent
        if (NewProjectTask.ProgressPercent < ProjectTaskConsts.ProgressPercentMinLength
            || NewProjectTask.ProgressPercent > ProjectTaskConsts.ProgressPercentMaxLength)
        {
            CreateFieldErrors["ProgressPercent"] = L["ProgressPercentRange"];
            if (isValid)
            {
                CreateGeneralValidationErrorKey = "ProgressPercentRange";
            }
            isValid = false;
        }

        return isValid;
    }

    private async Task FinishCreateWizardAsync()
    {
        if (IsFinishingWizard || !IsCreateWizardGeneralSaved)
        {
            return;
        }

        IsFinishingWizard = true;
        try
        {
            await InvokeAsync(StateHasChanged);

            if (CreateAssignmentsList.Count < 1)
            {
                await UiMessageService.Error(L["CreateWizard:AtLeastOneAssigneeRequired"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                SelectedCreateTab = "assignments";
                return;
            }

            // Reload kanban to reflect new task
            await RefreshKanbanAsync();
            await GetProjectTasksAsync();
            await CloseCreateProjectTaskModalAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsFinishingWizard = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task LoadCreateAssignmentsAsync()
    {
        if (CreateWizardProjectTaskId == Guid.Empty)
        {
            CreateAssignmentsList = new List<ProjectTaskAssignmentWithNavigationPropertiesDto>();
            return;
        }

        var result = await ProjectTaskAssignmentsAppService.GetListAsync(new GetProjectTaskAssignmentsInput
        {
            ProjectTaskId = CreateWizardProjectTaskId,
            MaxResultCount = 1000,
            SkipCount = 0
        });

        CreateAssignmentsList = result.Items;
    }

    private async Task LoadEditAssignmentsAsync()
    {
        if (EditingProjectTaskId == Guid.Empty)
        {
            EditAssignmentsList = new List<ProjectTaskAssignmentWithNavigationPropertiesDto>();
            return;
        }

        var result = await ProjectTaskAssignmentsAppService.GetListAsync(new GetProjectTaskAssignmentsInput
        {
            ProjectTaskId = EditingProjectTaskId,
            MaxResultCount = 1000,
            SkipCount = 0
        });

        EditAssignmentsList = result.Items;
    }

    private async Task LoadCreateDocumentsAsync()
    {
        if (CreateWizardProjectTaskId == Guid.Empty)
        {
            CreateDocumentsList = new List<ProjectTaskDocumentWithNavigationPropertiesDto>();
            return;
        }

        var result = await ProjectTaskDocumentsAppService.GetListAsync(new GetProjectTaskDocumentsInput
        {
            ProjectTaskId = CreateWizardProjectTaskId,
            MaxResultCount = 1000,
            SkipCount = 0
        });

        CreateDocumentsList = result.Items;
        
        // Cache PDF file info for each document
        await CacheDocumentPdfInfoAsync(CreateDocumentsList);
    }

    private async Task LoadEditDocumentsAsync()
    {
        if (EditingProjectTaskId == Guid.Empty)
        {
            EditDocumentsList = new List<ProjectTaskDocumentWithNavigationPropertiesDto>();
            return;
        }

        var result = await ProjectTaskDocumentsAppService.GetListAsync(new GetProjectTaskDocumentsInput
        {
            ProjectTaskId = EditingProjectTaskId,
            MaxResultCount = 1000,
            SkipCount = 0
        });

        EditDocumentsList = result.Items;
        
        // Cache PDF file info for each document
        await CacheDocumentPdfInfoAsync(EditDocumentsList);
    }

    protected async Task<List<LookupDto<Guid>>> GetAssignmentIdentityUserLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var result = await ProjectTaskAssignmentsAppService.GetIdentityUserLookupAsync(new LookupRequestDto
        {
            Filter = filter,
            MaxResultCount = 20,
            SkipCount = 0
        });

        AssignmentIdentityUsersCollection = result.Items;
        return result.Items.ToList();
    }

    protected void OnCreateAssignmentUserChanged()
    {
        // Select2 (single-select) uses list; force rerender so Add button enables.
        InvokeAsync(StateHasChanged);
    }

    protected void OnEditAssignmentUserChanged()
    {
        // Select2 (single-select) uses list; force rerender so Add button enables.
        InvokeAsync(StateHasChanged);
    }

    private async Task AddAssignmentAsync()
    {
        try
        {
            if (!IsCreateWizardGeneralSaved)
            {
                await UiMessageService.Error(L["CreateWizard:SaveGeneralFirst"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            var userId = CreateAssignmentsUsersToAdd.FirstOrDefault()?.Id ?? Guid.Empty;
            if (userId == Guid.Empty)
            {
                await UiMessageService.Error(L["CreateWizard:AssigneeRequired"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            await ProjectTaskAssignmentsAppService.CreateAsync(new ProjectTaskAssignmentCreateDto
            {
                ProjectTaskId = CreateWizardProjectTaskId,
                UserId = userId,
                AssignmentRole = CreateAssignmentRole.ToString(),
                AssignedAt = DateTime.Now,
                Note = CreateAssignmentNote
            });

            CreateAssignmentsUsersToAdd = new List<LookupDto<Guid>>();
            CreateAssignmentNote = null;
            await LoadCreateAssignmentsAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task AddEditAssignmentAsync()
    {
        try
        {
            if (EditingProjectTaskId == Guid.Empty)
            {
                return;
            }

            var userId = EditAssignmentsUsersToAdd.FirstOrDefault()?.Id ?? Guid.Empty;
            if (userId == Guid.Empty)
            {
                await UiMessageService.Error(L["CreateWizard:AssigneeRequired"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            await ProjectTaskAssignmentsAppService.CreateAsync(new ProjectTaskAssignmentCreateDto
            {
                ProjectTaskId = EditingProjectTaskId,
                UserId = userId,
                AssignmentRole = EditAssignmentRole.ToString(),
                AssignedAt = DateTime.Now,
                Note = EditAssignmentNote
            });

            EditAssignmentsUsersToAdd = new List<LookupDto<Guid>>();
            EditAssignmentNote = null;
            await LoadEditAssignmentsAsync();
            await RefreshEditingProjectTaskConcurrencyStampAsync();
            await RefreshKanbanAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task DeleteAssignmentAsync(ProjectTaskAssignmentWithNavigationPropertiesDto row)
    {
        try
        {
            await ProjectTaskAssignmentsAppService.DeleteAsync(row.ProjectTaskAssignment.Id);
            await LoadCreateAssignmentsAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task DeleteEditAssignmentAsync(ProjectTaskAssignmentWithNavigationPropertiesDto row)
    {
        try
        {
            await ProjectTaskAssignmentsAppService.DeleteAsync(row.ProjectTaskAssignment.Id);
            await LoadEditAssignmentsAsync();
            await RefreshEditingProjectTaskConcurrencyStampAsync();
            await RefreshKanbanAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

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

    protected void OnCreateDocumentChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    protected void OnEditDocumentChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private async Task AddDocumentAsync()
    {
        try
        {
            if (!IsCreateWizardGeneralSaved)
            {
                return;
            }

            var documentId = CreateDocumentsToAdd.FirstOrDefault()?.Id ?? Guid.Empty;
            if (documentId == Guid.Empty)
            {
                return;
            }

            await ProjectTaskDocumentsAppService.CreateAsync(new ProjectTaskDocumentCreateDto
            {
                ProjectTaskId = CreateWizardProjectTaskId,
                DocumentId = documentId,
                DocumentPurpose = CreateDocumentPurpose.ToString()
            });

            CreateDocumentsToAdd = new List<LookupDto<Guid>>();
            await LoadCreateDocumentsAsync();
            
            // Cache PDF info for newly added document
            if (documentId != Guid.Empty)
            {
                var hasPdf = await CheckIfDocumentHasPdfFileAsync(documentId);
                DocumentHasPdfCache[documentId] = hasPdf;
            }
            
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task AddEditDocumentAsync()
    {
        try
        {
            if (EditingProjectTaskId == Guid.Empty)
            {
                return;
            }

            var documentId = EditDocumentsToAdd.FirstOrDefault()?.Id ?? Guid.Empty;
            if (documentId == Guid.Empty)
            {
                return;
            }

            await ProjectTaskDocumentsAppService.CreateAsync(new ProjectTaskDocumentCreateDto
            {
                ProjectTaskId = EditingProjectTaskId,
                DocumentId = documentId,
                DocumentPurpose = EditDocumentPurpose.ToString()
            });

            EditDocumentsToAdd = new List<LookupDto<Guid>>();
            await LoadEditDocumentsAsync();
            
            // Cache PDF info for newly added document
            if (documentId != Guid.Empty)
            {
                var hasPdf = await CheckIfDocumentHasPdfFileAsync(documentId);
                DocumentHasPdfCache[documentId] = hasPdf;
            }
            
            await RefreshEditingProjectTaskConcurrencyStampAsync();
            await RefreshKanbanAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task DeleteDocumentAsync(ProjectTaskDocumentWithNavigationPropertiesDto row)
    {
        try
        {
            await ProjectTaskDocumentsAppService.DeleteAsync(row.ProjectTaskDocument.Id);
            
            // Clear cache for this document
            if (row.Document?.Id != null)
            {
                DocumentHasPdfCache.Remove(row.Document.Id);
            }
            
            await LoadCreateDocumentsAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task DeleteEditDocumentAsync(ProjectTaskDocumentWithNavigationPropertiesDto row)
    {
        try
        {
            await ProjectTaskDocumentsAppService.DeleteAsync(row.ProjectTaskDocument.Id);
            
            // Clear cache for this document
            if (row.Document?.Id != null)
            {
                DocumentHasPdfCache.Remove(row.Document.Id);
            }
            
            await LoadEditDocumentsAsync();
            await RefreshEditingProjectTaskConcurrencyStampAsync();
            await RefreshKanbanAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private void NavigateToProjectTaskDetail(ProjectTaskWithNavigationPropertiesDto input)
    {
        NavigationManager.NavigateTo($"/project-task-detail/{input.ProjectTask.Id}");
    }   

    private async Task OpenEditProjectTaskModalAsync(ProjectTaskWithNavigationPropertiesDto input, string? initialTab = null)
    {
        SelectedEditTab = initialTab ?? "general";
        var projectTask = await ProjectTasksAppService.GetWithNavigationPropertiesAsync(input.ProjectTask.Id);
        EditingProjectTaskId = projectTask.ProjectTask.Id;
        EditingProjectTask = ObjectMapper.Map<ProjectTaskDto, ProjectTaskUpdateDto>(projectTask.ProjectTask);
        EditGeneralValidationErrorKey = null;
        EditFieldErrors.Clear();

        // Initialize Select2 selections for Project and ParentTask.
        SelectedEditProjectTaskProject = new List<LookupDto<Guid>>();
        if (EditingProjectTask.ProjectId != Guid.Empty)
        {
            var displayName = projectTask.Project?.Name ?? string.Empty;
            var selectedProject = new LookupDto<Guid>
            {
                Id = EditingProjectTask.ProjectId,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? EditingProjectTask.ProjectId.ToString() : displayName
            };

            SelectedEditProjectTaskProject.Add(selectedProject);
            ProjectsCollection = ProjectsCollection.Concat(new[] { selectedProject }).DistinctBy(x => x.Id).ToList();
        }

        SelectedEditProjectTaskParentTask = new List<ParentTaskSelectItem>();
        EditParentTasksCollection = new List<ParentTaskSelectItem>();
        if (!string.IsNullOrWhiteSpace(EditingProjectTask.ParentTaskId))
        {
            var parentDisplayName = string.IsNullOrWhiteSpace(projectTask.ParentTaskTitle)
                ? EditingProjectTask.ParentTaskId!
                : $"{EditingProjectTask.ParentTaskId} - {projectTask.ParentTaskTitle}";
            var parent = new ParentTaskSelectItem
            {
                Id = EditingProjectTask.ParentTaskId!,
                DisplayName = parentDisplayName
            };
            SelectedEditProjectTaskParentTask.Add(parent);
            EditParentTasksCollection = EditParentTasksCollection.Concat(new[] { parent }).DistinctBy(x => x.Id).ToList();
        }
        EditParentTaskSelectKey = Guid.NewGuid();

        // Initialize enum selects from DTO strings.
        if (Enum.TryParse<ProjectTaskPriority>(EditingProjectTask.Priority, ignoreCase: true, out var priority))
        {
            EditingProjectTaskPriority = priority;
        }
        if (Enum.TryParse<ProjectTaskStatus>(EditingProjectTask.Status, ignoreCase: true, out var status))
        {
            EditingProjectTaskStatus = status;
        }

        // Load edit tabs data.
        EditAssignmentsUsersToAdd = new List<LookupDto<Guid>>();
        EditAssignmentRole = ProjectTaskAssignmentRole.MAIN;
        EditAssignmentNote = null;
        EditDocumentsToAdd = new List<LookupDto<Guid>>();
        EditDocumentPurpose = ProjectTaskDocumentPurpose.REPORT;
        await LoadEditAssignmentsAsync();
        await LoadEditDocumentsAsync();

        await EditProjectTaskModal.Show();
    }
    
    private async Task OpenEditProjectTaskModalToDocumentsTabAsync(ProjectTaskWithNavigationPropertiesDto input)
    {
        await OpenEditProjectTaskModalAsync(input, "documents");
    }

    protected async Task OnEditProjectTaskProjectChanged()
    {
        await Task.Yield();
        EditingProjectTask.ProjectId = SelectedEditProjectTaskProject.FirstOrDefault()?.Id ?? Guid.Empty;
        SelectedEditProjectTaskParentTask = new List<ParentTaskSelectItem>();
        EditingProjectTask.ParentTaskId = null;
        EditParentTasksCollection = new List<ParentTaskSelectItem>();
        EditParentTaskSelectKey = Guid.NewGuid();
        EditFieldErrors.Remove("Project");
        EditFieldErrors.Remove("ParentTaskId");

        // Preload parent tasks for the selected project.
        await GetEditParentTaskCollectionLookupAsync(EditParentTasksCollection, string.Empty, CancellationToken.None);
        await InvokeAsync(StateHasChanged);
    }

    protected void OnEditProjectTaskParentChanged()
    {
        EditingProjectTask.ParentTaskId = SelectedEditProjectTaskParentTask.FirstOrDefault()?.Id;
    }

    private async Task RefreshEditingProjectTaskConcurrencyStampAsync()
    {
        if (EditingProjectTaskId == Guid.Empty)
        {
            return;
        }

        var latestTask = await ProjectTasksAppService.GetWithNavigationPropertiesAsync(EditingProjectTaskId);
        EditingProjectTask.ConcurrencyStamp = latestTask.ProjectTask.ConcurrencyStamp;
    }

    protected void OnEditingProjectTaskPriorityChanged(ProjectTaskPriority priority)
    {
        EditingProjectTaskPriority = priority;
        EditingProjectTask.Priority = priority.ToString();
    }

    protected void OnEditingProjectTaskStatusChanged(ProjectTaskStatus status)
    {
        EditingProjectTaskStatus = status;
        EditingProjectTask.Status = status.ToString();
    }

    private async Task DeleteProjectTaskAsync(ProjectTaskWithNavigationPropertiesDto input)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            
            await ProjectTasksAppService.DeleteAsync(input.ProjectTask.Id);
            
            // Remove from AllKanbanItems
            AllKanbanItems.RemoveAll(x => x.Id == input.ProjectTask.Id);
            
            // Update counts
            var status = ParseStatus(input.ProjectTask.Status);
            if (KanbanTotalCounts.ContainsKey(status))
            {
                KanbanTotalCounts[status] = Math.Max(0, KanbanTotalCounts[status] - 1);
            }
            if (KanbanLoadedCounts.ContainsKey(status))
            {
                KanbanLoadedCounts[status] = Math.Max(0, KanbanLoadedCounts[status] - 1);
            }
            
            UpdateDisplayedKanbanItems();
            KanbanRenderKey++;
            await GetProjectTasksAsync();
            
            await BlockUiService.UnBlock();
            
            // Show success message
            await UiMessageService.Success(L["SuccessfullyDeleted"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await BlockUiService.UnBlock();
            await HandleErrorAsync(ex);
        }
    }

    private async Task DeleteProjectTaskWithConfirmationAsync(ProjectTaskWithNavigationPropertiesDto input)
    {
        if (await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value))
        {
            await DeleteProjectTaskAsync(input);
        }
    }

    private async Task CreateProjectTaskAsync()
    {
        try
        {
            if (!ValidateCreateGeneralInformation())
            {
                return;
            }

            var input = new ProjectTaskCreateDto
            {
                ParentTaskId = NewProjectTask.ParentTaskId,
                Code = NewProjectTask.Code,
                Title = NewProjectTask.Title,
                Description = NewProjectTask.Description,
                StartDate = NewProjectTask.StartDate,
                DueDate = NewProjectTask.DueDate,
                Priority = NewProjectTaskPriority.ToString(),
                Status = NewProjectTaskStatus.ToString(),
                ProgressPercent = NewProjectTask.ProgressPercent,
                ProjectId = NewProjectTask.ProjectId
            };

            var created = await ProjectTasksAppService.CreateAsync(input);
            
            // Reload kanban to include new task
            await RefreshKanbanAsync();
            await GetProjectTasksAsync();
            await CloseCreateProjectTaskModalAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task CloseEditProjectTaskModalAsync()
    {
        await EditProjectTaskModal.Hide();
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
                await UiMessageService.Warn(L[EditGeneralValidationErrorKey ?? "ValidationError"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                await InvokeAsync(StateHasChanged);
                return;
            }

            var updated = await ProjectTasksAppService.UpdateAsync(EditingProjectTaskId, EditingProjectTask);
            
            // Reload kanban to reflect updates
            await RefreshKanbanAsync();
            await GetProjectTasksAsync();
            await EditProjectTaskModal.Hide();
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
        // Reset error state.
        EditGeneralValidationErrorKey = null;
        EditFieldErrors.Clear();

        bool isValid = true;

        // Required: Project
        if (EditingProjectTask.ProjectId == Guid.Empty)
        {
            EditFieldErrors["Project"] = L["ProjectRequired"];
            EditGeneralValidationErrorKey = "ProjectRequired";
            isValid = false;
        }

        // Required: Code
        if (string.IsNullOrWhiteSpace(EditingProjectTask.Code))
        {
            EditFieldErrors["Code"] = L["CodeRequired"];
            if (isValid)
            {
                EditGeneralValidationErrorKey = "CodeRequired";
            }
            isValid = false;
        }

        // Required: Title
        if (string.IsNullOrWhiteSpace(EditingProjectTask.Title))
        {
            EditFieldErrors["Title"] = L["TitleRequired"];
            if (isValid)
            {
                EditGeneralValidationErrorKey = "TitleRequired";
            }
            isValid = false;
        }

        // Required: Priority/Status are strings on DTO (enum-backed selects fill them).
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

        // Range: ProgressPercent
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

        // DueDate must not be before StartDate (allow same day)
        if (EditingProjectTask.DueDate < EditingProjectTask.StartDate)
        {
            EditFieldErrors["DueDate"] = L["EndDateMustNotBeBeforeStartDate"];
            if (isValid)
            {
                EditGeneralValidationErrorKey = "EndDateMustNotBeBeforeStartDate";
            }
            isValid = false;
        }

        return isValid;
    }

    /// <summary>
    /// Handle ProgressPercent change - auto-set status to Done when progress reaches 100%
    /// </summary>
    private void OnNewProjectTaskProgressPercentChanged(int value)
    {
        NewProjectTask.ProgressPercent = value;

        // Auto-set status to Done when progress reaches 100%
        if (value == 100)
        {
            NewProjectTaskStatus = ProjectTaskStatus.DONE;
            // Also update the DTO string
            NewProjectTask.Status = ProjectTaskStatus.DONE.ToString();
        }

        CreateFieldErrors.Remove("ProgressPercent");
    }

    /// <summary>
    /// Handle ProgressPercent change for editing - auto-set status to Done when progress reaches 100%
    /// </summary>
    private void OnEditingProjectTaskProgressPercentChanged(int value)
    {
        EditingProjectTask.ProgressPercent = value;

        // Auto-set status to Done when progress reaches 100%
        if (value == 100)
        {
            EditingProjectTaskStatus = ProjectTaskStatus.DONE;
            // Also update the DTO string
            EditingProjectTask.Status = ProjectTaskStatus.DONE.ToString();
        }

        EditFieldErrors.Remove("ProgressPercent");
    }
}