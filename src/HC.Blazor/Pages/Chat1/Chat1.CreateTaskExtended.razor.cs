using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HC.Chat.Conversations;
using HC.Chat.Messages;
using HC.ProjectTasks;
using HC.ProjectTaskAssignments;
using HC.Shared;
using Microsoft.Extensions.Logging;
using Blazorise;

namespace HC.Blazor.Pages.Chat1;

/// <summary>
/// Partial class for creating task from message functionality
/// </summary>
public partial class Chat1
{
    // Create task from message state - Following ProjectTasks pattern
    protected bool ShowCreateTaskFromMessageModal { get; set; }
    protected ChatMessageDto TaskSourceMessage { get; set; }
    protected ProjectTaskDto NewTaskFromMessage { get; set; } = new();
    protected ProjectTaskPriority NewTaskFromMessagePriority { get; set; } = ProjectTaskPriority.LOW;
    protected ProjectTaskStatus NewTaskFromMessageStatus { get; set; } = ProjectTaskStatus.TODO;
    
    // Wizard state (like ProjectTasks)
    protected string SelectedCreateTaskTab { get; set; } = "general";
    protected bool IsTaskGeneralSaved { get; set; }
    protected bool IsSavingTaskGeneral { get; set; }
    protected Guid CreatedTaskId { get; set; }
    
    private DatePicker<DateTime>? NewTaskFromMessageStartDateDatePicker { get; set; }
    private DatePicker<DateTime>? NewTaskFromMessageDueDateDatePicker { get; set; }
    protected Dictionary<string, string> CreateTaskFieldErrors { get; set; } = new();
    protected string CreateTaskGeneralValidationErrorKey { get; set; }
    
    // Select2 collections
    protected List<LookupDto<Guid>> SelectedTaskProject { get; set; } = new();
    protected List<TaskLookupItem> SelectedTaskParent { get; set; } = new();
    protected IReadOnlyList<LookupDto<Guid>> TaskProjectsCollection { get; set; } = new List<LookupDto<Guid>>();
    protected IReadOnlyList<TaskLookupItem> ParentTasksForChatCollection { get; set; } = new List<TaskLookupItem>();
    
    // Assignments (like ProjectTasks)
    protected List<LookupDto<Guid>> CreateTaskAssignmentsUsersToAdd { get; set; } = new();
    protected ProjectTaskAssignmentRole CreateTaskAssignmentRole { get; set; } = ProjectTaskAssignmentRole.MAIN;
    protected string CreateTaskAssignmentNote { get; set; }
    protected List<ProjectTaskAssignmentWithNavigationPropertiesDto> CreateTaskAssignmentsList { get; set; } = new();
    
    /// <summary>
    /// Helper class for parent task selection
    /// </summary>
    public sealed class TaskLookupItem
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
    }
    
    /// <summary>
    /// Show create task from message modal
    /// </summary>
    protected async Task CreateTaskFromMessageAsync(ChatMessageDto message)
    {
        try
        {
            TaskSourceMessage = message;
            CreateTaskGeneralValidationErrorKey = null;
            ShowCreateTaskFromMessageModal = true;
            
            // Initialize new task with default values
            NewTaskFromMessage = new ProjectTaskDto
            {
                Code = await GenerateTaskCodeAsync(),
                Title = message.Message.TruncateWithPostfix(100, "..."),
                Description = $"[Công việc được tạo từ tin nhắn của {message.SenderSurname} {message.SenderName} - Ngày gửi {message.MessageDate.ToString("dd/MM/yyyy HH:mm")}] Nội dung tin nhắn: \n\n{message.Message}",
                Priority = ProjectTaskPriority.LOW.ToString(),
                Status = ProjectTaskStatus.TODO.ToString(),
                ProgressPercent = 0,
                StartDate = DateTime.Now,
                DueDate = DateTime.Now.AddDays(7)
            };
            
            NewTaskFromMessagePriority = ProjectTaskPriority.LOW;
            NewTaskFromMessageStatus = ProjectTaskStatus.TODO;
            
            // Reset selections
            SelectedTaskProject = new List<LookupDto<Guid>>();
            SelectedTaskParent = new List<TaskLookupItem>();
            CreateTaskAssignmentsUsersToAdd = new List<LookupDto<Guid>>();
            
            // Reset wizard state
            SelectedCreateTaskTab = "general";
            IsTaskGeneralSaved = false;
            IsSavingTaskGeneral = false;
            CreatedTaskId = Guid.Empty;
            CreateTaskFieldErrors.Clear();
            CreateTaskGeneralValidationErrorKey = null;
            CreateTaskAssignmentsList = new List<ProjectTaskAssignmentWithNavigationPropertiesDto>();
            CreateTaskAssignmentRole = ProjectTaskAssignmentRole.MAIN;
            CreateTaskAssignmentNote = string.Empty;
            
            // Load project list
            await GetTaskProjectCollectionLookupAsync();
            await GetIdentityUserCollectionLookupAsync();
            
            // Auto-select project if current conversation is a Project conversation
            if (CurrentChatContact?.Type == ConversationType.Project && CurrentChatContact.ProjectId.HasValue)
            {
                var project = TaskProjectsCollection.FirstOrDefault(p => p.Id == CurrentChatContact.ProjectId.Value);
                if (project != null)
                {
                    SelectedTaskProject = new List<LookupDto<Guid>> { project };
                    NewTaskFromMessage.ProjectId = project.Id;
                }
            }
            
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }
    
    /// <summary>
    /// Generate a task code
    /// </summary>
    private async Task<string> GenerateTaskCodeAsync()
    {
        try
        {
            int maxNumber = 0;
            const int pageSize = 100;
            int skipCount = 0;
            bool hasMore = true;
            
            while (hasMore)
            {
                var input = new GetProjectTasksInput
                {
                    MaxResultCount = pageSize,
                    SkipCount = skipCount,
                    Sorting = "ProjectTask.Code DESC"
                };
                
                var result = await ProjectTasksAppService.GetListAsync(input);
                
                if (result.Items == null || result.Items.Count == 0)
                {
                    hasMore = false;
                    break;
                }
                
                foreach (var task in result.Items)
                {
                    if (!string.IsNullOrWhiteSpace(task.ProjectTask.Code))
                    {
                        var code = task.ProjectTask.Code.Trim();
                        if (code.StartsWith("T", StringComparison.OrdinalIgnoreCase) && code.Length > 1)
                        {
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
                
                if (result.Items.Count < pageSize || skipCount + pageSize >= result.TotalCount)
                {
                    hasMore = false;
                }
                else
                {
                    skipCount += pageSize;
                }
            }
            
            return $"T{(maxNumber + 1):D7}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating task code");
            return $"T{DateTime.Now.Ticks % 1000000:D6}";
        }
    }
    
    /// <summary>
    /// Handle project selection change
    /// </summary>
    private void OnTaskProjectChanged()
    {
        NewTaskFromMessage.ProjectId = SelectedTaskProject.FirstOrDefault()?.Id ?? Guid.Empty;
    }
    
    /// <summary>
    /// Load projects for task creation
    /// </summary>
    private async Task GetTaskProjectCollectionLookupAsync(string newValue = null)
    {
        TaskProjectsCollection = (await ProjectTasksAppService.GetProjectLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }
    
    /// <summary>
    /// Load parent tasks for task creation
    /// </summary>
    private async Task<List<TaskLookupItem>> GetParentTaskForChatLookupAsync(IReadOnlyList<TaskLookupItem> dbset, string filter, CancellationToken token)
    {
        var input = new GetProjectTasksInput
        {
            FilterText = filter,
            MaxResultCount = 20,
            SkipCount = 0
        };
        
        var result = await ProjectTasksAppService.GetListAsync(input);
        ParentTasksForChatCollection = result.Items
            .Select(x => new TaskLookupItem
            {
                Id = x.ProjectTask.Code,
                DisplayName = $"{x.ProjectTask.Code} - {x.ProjectTask.Title}"
            })
            .ToList();
        
        return ParentTasksForChatCollection.ToList();
    }
    
    /// <summary>
    /// Helper methods for field validation (like ProjectTasks)
    /// </summary>
    private bool HasCreateTaskFieldError(string fieldName)
    {
        return CreateTaskFieldErrors.ContainsKey(fieldName);
    }
    
    private string GetCreateTaskFieldError(string fieldName)
    {
        return CreateTaskFieldErrors.TryGetValue(fieldName, out var error) ? error : string.Empty;
    }
    
    /// <summary>
    /// Validate task creation form - Manual validation like ProjectTasks
    /// </summary>
    private bool ValidateCreateTaskGeneralInformation()
    {
        CreateTaskFieldErrors.Clear();
        CreateTaskGeneralValidationErrorKey = null;
        
        // Validate Project
        if (NewTaskFromMessage.ProjectId == Guid.Empty || !SelectedTaskProject.Any())
        {
            CreateTaskFieldErrors["Project"] = L["PleaseSelectAProject"];
            CreateTaskGeneralValidationErrorKey = "PleaseSelectAProject";
            return false;
        }
        
        // Validate Code
        if (string.IsNullOrWhiteSpace(NewTaskFromMessage.Code))
        {
            CreateTaskFieldErrors["Code"] = L["CodeRequired"];
            CreateTaskGeneralValidationErrorKey = "CodeRequired";
            return false;
        }
        
        if (NewTaskFromMessage.Code.Length > 50)
        {
            CreateTaskFieldErrors["Code"] = L["CodeMaxLength"];
            CreateTaskGeneralValidationErrorKey = "CodeMaxLength";
            return false;
        }
        
        // Validate Title
        if (string.IsNullOrWhiteSpace(NewTaskFromMessage.Title))
        {
            CreateTaskFieldErrors["Title"] = L["TitleRequired"];
            CreateTaskGeneralValidationErrorKey = "TitleRequired";
            return false;
        }
        
        if (NewTaskFromMessage.Title.Length > 256)
        {
            CreateTaskFieldErrors["Title"] = L["TitleMaxLength"];
            CreateTaskGeneralValidationErrorKey = "TitleMaxLength";
            return false;
        }
        
        // Validate Progress Percent
        if (NewTaskFromMessage.ProgressPercent < 0 || NewTaskFromMessage.ProgressPercent > 100)
        {
            CreateTaskFieldErrors["ProgressPercent"] = L["ProgressPercentMustBeBetween0And100"];
            CreateTaskGeneralValidationErrorKey = "ProgressPercentMustBeBetween0And100";
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Handle tab selection change
    /// </summary>
    private async Task OnSelectedCreateTaskTabChanged(string tabName)
    {
        SelectedCreateTaskTab = tabName;
        await InvokeAsync(StateHasChanged);
    }
    
    /// <summary>
    /// Save general information (Step 1 of wizard) - Like ProjectTasks
    /// </summary>
    private async Task SaveTaskGeneralInformationAsync()
    {
        if (!ValidateCreateTaskGeneralInformation())
        {
            await InvokeAsync(StateHasChanged);
            return;
        }
        
        try
        {
            IsSavingTaskGeneral = true;
            await InvokeAsync(StateHasChanged);
            
            // Create the task
            var createInput = new ProjectTaskCreateDto
            {
                Code = NewTaskFromMessage.Code,
                Title = NewTaskFromMessage.Title,
                Description = NewTaskFromMessage.Description,
                ParentTaskId = SelectedTaskParent.FirstOrDefault()?.Id,
                Priority = NewTaskFromMessagePriority.ToString(),
                Status = NewTaskFromMessageStatus.ToString(),
                ProgressPercent = NewTaskFromMessage.ProgressPercent,
                StartDate = NewTaskFromMessage.StartDate,
                DueDate = NewTaskFromMessage.DueDate,
                ProjectId = NewTaskFromMessage.ProjectId
            };
            
            var createdTask = await ProjectTasksAppService.CreateAsync(createInput);
            CreatedTaskId = createdTask.Id;
            IsTaskGeneralSaved = true;
            CreateTaskFieldErrors.Clear();
            CreateTaskGeneralValidationErrorKey = null;
            
            // Move to assignments tab
            SelectedCreateTaskTab = "assignments";
        }
        catch (Exception ex)
        {
            CreateTaskGeneralValidationErrorKey = "UnexpectedError";
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsSavingTaskGeneral = false;
            await InvokeAsync(StateHasChanged);
        }
    }
    
    /// <summary>
    /// Handle assignment user selection change
    /// </summary>
    private void OnCreateTaskAssignmentUserChanged()
    {
        InvokeAsync(StateHasChanged);
    }
    
    /// <summary>
    /// Add assignee to created task (Like ProjectTasks - add single user with role)
    /// </summary>
    private async Task AddTaskAssignmentAsync()
    {
        if (!CreateTaskAssignmentsUsersToAdd.Any())
        {
            return;
        }
        
        try
        {
            var userId = CreateTaskAssignmentsUsersToAdd.First().Id;
            
            // Create assignment
            var assignment = await ProjectTaskAssignmentsAppService.CreateAsync(new ProjectTaskAssignmentCreateDto
            {
                ProjectTaskId = CreatedTaskId,
                UserId = userId,
                AssignmentRole = CreateTaskAssignmentRole.ToString(),
                AssignedAt = DateTime.Now,
                Note = CreateTaskAssignmentNote
            });
            
            // Get full assignment with user info
            var assignmentWithNav = await ProjectTaskAssignmentsAppService.GetWithNavigationPropertiesAsync(assignment.Id);
            CreateTaskAssignmentsList.Add(assignmentWithNav);
            
            // Clear selection
            CreateTaskAssignmentsUsersToAdd = new List<LookupDto<Guid>>();
            CreateTaskAssignmentNote = string.Empty;
            
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }
    
    /// <summary>
    /// Delete assignment from list
    /// </summary>
    private async Task DeleteTaskAssignmentAsync(ProjectTaskAssignmentWithNavigationPropertiesDto assignment)
    {
        try
        {
            await ProjectTaskAssignmentsAppService.DeleteAsync(assignment.ProjectTaskAssignment.Id);
            CreateTaskAssignmentsList.Remove(assignment);
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }
    
    /// <summary>
    /// Finish wizard and close modal
    /// </summary>
    private async Task FinishCreateTaskWizardAsync()
    {
        CloseCreateTaskFromMessageModal();
        await InvokeAsync(StateHasChanged);
    }
    
    /// <summary>
    /// Close create task modal (Like ProjectTasks CancelCreateWizardAsync)
    /// </summary>
    private void CloseCreateTaskFromMessageModal()
    {
        ShowCreateTaskFromMessageModal = false;
        TaskSourceMessage = null;
        NewTaskFromMessage = new ProjectTaskDto();
        SelectedTaskProject = new List<LookupDto<Guid>>();
        SelectedTaskParent = new List<TaskLookupItem>();
        
        // Reset wizard state
        SelectedCreateTaskTab = "general";
        IsTaskGeneralSaved = false;
        IsSavingTaskGeneral = false;
        CreatedTaskId = Guid.Empty;
        CreateTaskFieldErrors.Clear();
        CreateTaskGeneralValidationErrorKey = null;
        
        // Reset assignments
        CreateTaskAssignmentsUsersToAdd = new List<LookupDto<Guid>>();
        CreateTaskAssignmentRole = ProjectTaskAssignmentRole.MAIN;
        CreateTaskAssignmentNote = string.Empty;
        CreateTaskAssignmentsList = new List<ProjectTaskAssignmentWithNavigationPropertiesDto>();
        
        InvokeAsync(StateHasChanged);
    }
}
