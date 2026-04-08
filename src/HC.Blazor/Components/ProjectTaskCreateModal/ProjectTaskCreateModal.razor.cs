


using Blazorise;
using HC.ProjectTasks;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using HC.Shared;
using System.Linq;
using Volo.Abp.Application.Dtos;
using HC.ProjectTaskAssignments;
using HC.ProjectTaskDocuments;
using System.Threading;
using System.IO;
using HC.DocumentFiles;
using Volo.Abp.AspNetCore.Components.Messages;
namespace HC.Blazor.Components.ProjectTaskCreateModal;

public partial class ProjectTaskCreateModal
{
    // Parameters for external use
    [Parameter] public Guid? ProjectId { get; set; }
    [Parameter] public EventCallback OnTaskCreated { get; set; }

    public sealed class CreateProjectTaskModalOptions
    {
        public Guid? ProjectId { get; init; }
        public string? Title { get; init; }
        public string? Description { get; init; }
        public string? ParentTaskId { get; init; }
        public ProjectTaskPriority? Priority { get; init; }
        public ProjectTaskStatus? Status { get; init; }
        public int? ProgressPercent { get; init; }
        public DateTime? StartDate { get; init; }
        public DateTime? DueDate { get; init; }
        public bool RequireAtLeastOneAssignee { get; init; } = true;
    }

    private Modal CreateProjectTaskModal { get; set; } = new();
    private Guid? EffectiveProjectId { get; set; }
    private bool RequireAtLeastOneAssignee { get; set; } = true;
    protected string SelectedCreateTab = "general";
    protected bool IsNavigatingTab { get; set; }
    private bool IsSavingGeneralInformation { get; set; } = false;
    private bool IsFinishingWizard { get; set; } = false;

    protected Guid CreateWizardProjectTaskId { get; set; }
    private Guid EditingProjectTaskId { get; set; }
    private ProjectTaskDto NewProjectTask { get; set; } = new();
    private ProjectTaskUpdateDto EditingProjectTask { get; set; } = new();
    protected bool IsCreateWizardGeneralSaved => CreateWizardProjectTaskId != Guid.Empty;
    private string? CreateGeneralValidationErrorKey { get; set; }
    private IReadOnlyList<LookupDto<Guid>> ProjectsCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<LookupDto<Guid>> SelectedNewProjectTaskProject { get; set; } = new();
    private List<ParentTaskSelectItem> SelectedNewProjectTaskParentTask { get; set; } = new();
    private ProjectTaskPriority NewProjectTaskPriority { get; set; } = ProjectTaskPriority.LOW;
    private ProjectTaskStatus NewProjectTaskStatus { get; set; } = ProjectTaskStatus.TODO;
    private IReadOnlyList<ParentTaskSelectItem> ParentTasksCollection { get; set; } = new List<ParentTaskSelectItem>();
    private Guid ParentTaskSelectKey { get; set; } = Guid.NewGuid(); // Key to force re-render when project changes
    private DatePicker<DateTime>? NewProjectTaskStartDateDatePicker { get; set; }
    private DatePicker<DateTime>? NewProjectTaskDueDateDatePicker { get; set; }

    private Dictionary<Guid, bool> DocumentHasPdfCache { get; set; } = new();
    private Guid? PendingPrimaryDocumentId { get; set; }


    private IReadOnlyList<ProjectTaskAssignmentWithNavigationPropertiesDto> CreateAssignmentsList { get; set; } = new List<ProjectTaskAssignmentWithNavigationPropertiesDto>();
    private IReadOnlyList<LookupDto<Guid>> AssignmentIdentityUsersCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<LookupDto<Guid>> CreateAssignmentsUsersToAdd { get; set; } = new();
    private ProjectTaskAssignmentRole CreateAssignmentRole { get; set; } = ProjectTaskAssignmentRole.MAIN;
    private string? CreateAssignmentNote { get; set; }

    private Dictionary<string, string?> CreateFieldErrors { get; set; } = new();

    private string? GetCreateFieldError(string fieldName) => CreateFieldErrors.GetValueOrDefault(fieldName);
    private bool HasCreateFieldError(string fieldName) => CreateFieldErrors.ContainsKey(fieldName) && !string.IsNullOrWhiteSpace(CreateFieldErrors[fieldName]);



    private IReadOnlyList<ProjectTaskDocumentWithNavigationPropertiesDto> CreateDocumentsList { get; set; } = new List<ProjectTaskDocumentWithNavigationPropertiesDto>();
    private IReadOnlyList<LookupDto<Guid>> DocumentsLookupCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<LookupDto<Guid>> CreateDocumentsToAdd { get; set; } = new();
    private ProjectTaskDocumentPurpose CreateDocumentPurpose { get; set; } = ProjectTaskDocumentPurpose.REPORT;

    private async Task GetProjectCollectionLookupAsync(string? newValue = null)
    {
        ProjectsCollection = (await ProjectTasksAppService.GetProjectLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }

    private async Task<List<LookupDto<Guid>>> GetProjectCollectionLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        ProjectsCollection = (await ProjectTasksAppService.GetProjectLookupAsync(new LookupRequestDto { Filter = filter })).Items;
        return ProjectsCollection.ToList();
    }

    private async Task<List<ParentTaskSelectItem>> GetParentTaskCollectionLookupAsync(IReadOnlyList<ParentTaskSelectItem> dbset, string filter, CancellationToken token)
    {
        // Get the current project ID - from parameter, DTO, or user selection (in order of priority)
        var currentProjectId = Guid.Empty;
        
        // Priority 1: From component parameter (when opened from ProjectDetail page)
        if (EffectiveProjectId.HasValue && EffectiveProjectId.Value != Guid.Empty)
        {
            currentProjectId = EffectiveProjectId.Value;
        }
        // Priority 2: From NewProjectTask DTO (already set)
        else if (NewProjectTask.ProjectId != Guid.Empty)
        {
            currentProjectId = NewProjectTask.ProjectId;
        }
        // Priority 3: From selected project in dropdown (in case DTO not yet updated)
        else if (SelectedNewProjectTaskProject.Any())
        {
            currentProjectId = SelectedNewProjectTaskProject.First().Id;
        }

        // If no project is selected, return empty list (parent task should only be from the same project)
        if (currentProjectId == Guid.Empty)
        {
            ParentTasksCollection = new List<ParentTaskSelectItem>();
            return new List<ParentTaskSelectItem>();
        }

        var input = new GetProjectTasksInput
        {
            FilterText = filter,
            MaxResultCount = 20,
            SkipCount = 0,
            ProjectId = currentProjectId, // Filter by current project
        };

        // UI-first: use Code as parent id (string) because DTO uses ParentTaskId as string.
        var result = await ProjectTasksAppService.GetListAsync(input);
        ParentTasksCollection = result.Items
            // Prevent selecting itself as parent when editing.
            .Where(x => EditingProjectTaskId == Guid.Empty
                || (x.ProjectTask.Id != EditingProjectTaskId
                    && !string.Equals(x.ProjectTask.Code, EditingProjectTask.Code, StringComparison.OrdinalIgnoreCase)))
            .Select(x => new ParentTaskSelectItem
            {
                Id = x.ProjectTask.Code,
                DisplayName = $"{x.ProjectTask.Code} - {x.ProjectTask.Title}",
            })
            .ToList();

        return ParentTasksCollection.ToList();
    }

    protected async Task OnNewProjectTaskProjectChanged()
    {
        await Task.Yield();

        NewProjectTask.ProjectId = SelectedNewProjectTaskProject.FirstOrDefault()?.Id ?? Guid.Empty;
        
        SelectedNewProjectTaskParentTask = new List<ParentTaskSelectItem>();
        NewProjectTask.ParentTaskId = null;
        ParentTasksCollection = new List<ParentTaskSelectItem>();
        
        ParentTaskSelectKey = Guid.NewGuid();

        await InvokeAsync(StateHasChanged);
    }

    protected void OnNewProjectTaskParentChanged()
    {
        NewProjectTask.ParentTaskId = SelectedNewProjectTaskParentTask.FirstOrDefault()?.Id;
    }

    protected void OnNewProjectTaskPriorityChanged(ProjectTaskPriority priority)
    {
        NewProjectTaskPriority = priority;
        NewProjectTask.Priority = priority.ToString();
    }

    protected void OnNewProjectTaskStatusChanged(ProjectTaskStatus status)
    {
        NewProjectTaskStatus = status;
        NewProjectTask.Status = status.ToString();
    }

    
    private async Task OnSelectedCreateTabChanged(string name)
    {
        if (IsNavigatingTab)
        {
            return;
        }

        if ((name == "assignments" || name == "documents") && !IsCreateWizardGeneralSaved)
        {
            SelectedCreateTab = "general";
            return;
        }

        IsNavigatingTab = true;
        try
        {
            await InvokeAsync(StateHasChanged);
            await Task.Delay(100); // Small delay for smooth transition
            SelectedCreateTab = name;
            await InvokeAsync(StateHasChanged);
        }
        finally
        {
            IsNavigatingTab = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    
    public Task OpenCreateProjectTaskModalAsync(Guid? primaryDocumentId = null)
    {
        return OpenCreateProjectTaskModalInternalAsync(primaryDocumentId, null);
    }

    public Task OpenCreateProjectTaskModalAsync(CreateProjectTaskModalOptions options, Guid? primaryDocumentId = null)
    {
        return OpenCreateProjectTaskModalInternalAsync(primaryDocumentId, options);
    }

    private async Task OpenCreateProjectTaskModalInternalAsync(Guid? primaryDocumentId, CreateProjectTaskModalOptions? options)
    {
        PendingPrimaryDocumentId = primaryDocumentId;

        EffectiveProjectId = options?.ProjectId ?? ProjectId;
        RequireAtLeastOneAssignee = options?.RequireAtLeastOneAssignee ?? true;

        var startDate = options?.StartDate ?? DateTime.Now;
        var dueDate = options?.DueDate ?? startDate;
        var priority = options?.Priority ?? ProjectTaskPriority.LOW;
        var status = options?.Status ?? ProjectTaskStatus.TODO;

        NewProjectTask = new ProjectTaskDto
        {
            Title = options?.Title ?? string.Empty,
            Description = options?.Description ?? string.Empty,
            ParentTaskId = options?.ParentTaskId,
            ProgressPercent = options?.ProgressPercent ?? 0,
            StartDate = startDate,
            DueDate = dueDate,
            Priority = priority.ToString(),
            Status = status.ToString(),
            Code = await GenerateNextProjectTaskCodeAsync(), // Auto-generate code
        };

        // If ProjectId is provided by parameter or open options.
        if (EffectiveProjectId.HasValue && EffectiveProjectId.Value != Guid.Empty)
        {
            NewProjectTask.ProjectId = EffectiveProjectId.Value;
        }

        // Defaults for enum-backed selects.
        NewProjectTaskPriority = priority;
        NewProjectTaskStatus = status;

        SelectedNewProjectTaskProject = new List<LookupDto<Guid>>();
        SelectedNewProjectTaskParentTask = new List<ParentTaskSelectItem>();
        ParentTasksCollection = new List<ParentTaskSelectItem>();
        ParentTaskSelectKey = Guid.NewGuid();

        CreateWizardProjectTaskId = Guid.Empty;
        CreateGeneralValidationErrorKey = null;
        CreateFieldErrors.Clear();
        CreateAssignmentsUsersToAdd = new List<LookupDto<Guid>>();
        CreateAssignmentsList = new List<ProjectTaskAssignmentWithNavigationPropertiesDto>();
        CreateAssignmentRole = ProjectTaskAssignmentRole.MAIN;
        CreateAssignmentNote = null;
        CreateDocumentsToAdd = new List<LookupDto<Guid>>();
        CreateDocumentsList = new List<ProjectTaskDocumentWithNavigationPropertiesDto>();
        CreateDocumentPurpose = ProjectTaskDocumentPurpose.REPORT;
        SelectedCreateTab = "general";

        // Only load projects if ProjectId is not provided
        if (!EffectiveProjectId.HasValue || EffectiveProjectId.Value == Guid.Empty)
        {
            await GetProjectCollectionLookupAsync();
        }
        else
        {
            // Pre-select the project when opening from ProjectDetail or a custom open option
            var projectLookup = await ProjectTasksAppService.GetProjectLookupAsync(new LookupRequestDto { MaxResultCount = 200 });
            var currentProject = projectLookup.Items.FirstOrDefault(p => p.Id == EffectiveProjectId.Value);
            if (currentProject != null)
            {
                ProjectsCollection = projectLookup.Items;
                SelectedNewProjectTaskProject = new List<LookupDto<Guid>> { currentProject };
            }
        }

        await CreateProjectTaskModal.Show();
    }
    
    // Generate next available ProjectTask code (Txxxxxx format)
    private async Task<string> GenerateNextProjectTaskCodeAsync()
    {
        try
        {
            int maxNumber = 0;
            const int pageSize = 200; // Process in batches
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
        catch
        {
            return "T000001";
        }
    }

    private async Task CloseCreateProjectTaskModalAsync()
    {
        EffectiveProjectId = ProjectId;
        RequireAtLeastOneAssignee = true;
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
        await CreateProjectTaskModal.Hide();
    }

    private async Task CancelCreateWizardAsync()
    {
        try
        {
            if (CreateWizardProjectTaskId != Guid.Empty)
            {
                if (!await UiMessageService.Confirm(L["CreateWizard:CancelAndDeleteTask"].Value,
                options: new Action<UiMessageOptions>(options => options.ConfirmButtonText = L["Confirm"])))
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
            await AttachPendingPrimaryDocumentAsync();

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

    private async Task AttachPendingPrimaryDocumentAsync()
    {
        if (CreateWizardProjectTaskId == Guid.Empty || !PendingPrimaryDocumentId.HasValue || PendingPrimaryDocumentId.Value == Guid.Empty)
        {
            return;
        }

        var primaryDocumentId = PendingPrimaryDocumentId.Value;
        if (CreateDocumentsList.Any(x => x.Document?.Id == primaryDocumentId))
        {
            return;
        }

        await ProjectTaskDocumentsAppService.CreateAsync(new ProjectTaskDocumentCreateDto
        {
            ProjectTaskId = CreateWizardProjectTaskId,
            DocumentId = primaryDocumentId,
            DocumentPurpose = ProjectTaskDocumentPurpose.REFERENCE.ToString()
        });

        await LoadCreateDocumentsAsync();
        PendingPrimaryDocumentId = null;
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

        // DueDate must not be before StartDate (allow same day)
        if (NewProjectTask.DueDate < NewProjectTask.StartDate)
        {
            CreateFieldErrors["DueDate"] = L["EndDateMustNotBeBeforeStartDate"];
            if (isValid)
            {
                CreateGeneralValidationErrorKey = "EndDateMustNotBeBeforeStartDate";
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

            if (RequireAtLeastOneAssignee && CreateAssignmentsList.Count < 1)
            {
                await UiMessageService.Error(L["CreateWizard:AtLeastOneAssigneeRequired"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                SelectedCreateTab = "assignments";
                return;
            }
            await CloseCreateProjectTaskModalAsync();
            await UiMessageService.Success(L["TaskCreatedSuccessfully"],
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            
            // Notify parent component that task was created
            if (OnTaskCreated.HasDelegate)
            {
                await OnTaskCreated.InvokeAsync();
            }
            
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
            MaxResultCount = 200,
            SkipCount = 0
        });

        CreateAssignmentsList = result.Items;
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
            MaxResultCount = 200,
            SkipCount = 0
        });

        CreateDocumentsList = result.Items;
        
        await CacheDocumentPdfInfoAsync(CreateDocumentsList);
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
        InvokeAsync(StateHasChanged);
    }


    private async Task AddAssignmentAsync()
    {
        try
        {
            if (!IsCreateWizardGeneralSaved)
            {
                await UiMessageService.Error(L["CreateWizard:SaveGeneralFirst"]);
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
            
            await CloseCreateProjectTaskModalAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

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
    
    private async Task<bool> CheckIfDocumentHasPdfFileAsync(Guid documentId)
    {
        try
        {
            var documentFilesResult = await DocumentFilesAppService.GetListAsync(new GetDocumentFilesInput
            {
                DocumentId = documentId,
                MaxResultCount = 1,
                SkipCount = 0
            });
            
            if (documentFilesResult.Items == null || !documentFilesResult.Items.Any())
            {
                return false;
            }
            
            var documentFile = documentFilesResult.Items.First();
            return IsPdfFileExtension(documentFile.DocumentFile.Name) && !string.IsNullOrEmpty(documentFile.DocumentFile.Path);
        }
        catch
        {
            return false;
        }
    }

    
    private async Task CacheDocumentPdfInfoAsync(IReadOnlyList<ProjectTaskDocumentWithNavigationPropertiesDto> documents)
    {
        foreach (var doc in documents)
        {
            if (doc?.Document?.Id == null || DocumentHasPdfCache.ContainsKey(doc.Document.Id))
            {
                continue;
            }
            
            var hasPdf = await CheckIfDocumentHasPdfFileAsync(doc.Document.Id);
            DocumentHasPdfCache[doc.Document.Id] = hasPdf;
        }
    }

    private bool IsPdfFileExtension(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;
        
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension == ".pdf";
    }

    protected sealed class ParentTaskSelectItem
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
    }
}

