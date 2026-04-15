using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Blazorise;
using Blazorise.DataGrid;
using Volo.Abp.BlazoriseUI.Components;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Components.Web.Theming.PageToolbars;
using HC.Workflows;
using HC.WorkflowTemplates;
using HC.WorkflowStepTemplates;
using HC.WorkflowStepAssignments;
using HC.Permissions;
using HC.Shared;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Volo.Abp;
using Volo.Abp.Content;
using Microsoft.Extensions.Caching.Memory;
using Volo.Abp.Identity;
using Microsoft.AspNetCore.Components.Routing;
using Volo.Abp.AspNetCore.Components.Messages;
using HC.Localization;
using HC.Blazor.Shared;
using Microsoft.Extensions.Logging;
using Volo.Abp.BlobStoring;
using Blazorise.Extensions;
using Blazorise.PdfViewer;
using System.IO;
using HC.Blazor.Services.DocxToPdfConverter;
namespace HC.Blazor.Pages.Workflows;

public partial class WorkflowDetail : ValidationPageBase, IDisposable
{
    [Inject] private IMemoryCache __MemoryCache { get; set; } = default!;
    [Inject] private IWorkflowsAppService WorkflowsAppService { get; set; } = default!;
    [Inject] private IWorkflowTemplatesAppService WorkflowTemplatesAppService { get; set; } = default!;
    [Inject] private IWorkflowStepTemplatesAppService WorkflowStepTemplatesAppService { get; set; } = default!;
    [Inject] private IWorkflowStepAssignmentsAppService WorkflowStepAssignmentsAppService { get; set; } = default!;
    [Inject] private IUiMessageService UiMessageService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<WorkflowDetail> Logger { get; set; } = default!;
    [Inject] private IBlobContainer BlobContainer { get; set; } = default!;
    [Inject] private HC.DocumentPdfViewer.IDocumentPdfViewerAppService DocumentPdfViewerAppService { get; set; } = default!;
    [Inject] private IDocxToPdfConverter DocxToPdfConverter { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter] public Guid? Id { get; set; }

    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();
    protected PageToolbar Toolbar { get; } = new PageToolbar();

    // Step management
    private Steps StepsRef { get; set; } = new();
    protected string SelectedStep { get; set; } = "step1";
    private bool IsWorkflowSaved { get; set; }
    private bool IsTemplateSaved { get; set; }
    private bool HasWorkflowSteps { get; set; }
    private bool HasWorkflowStepAssignments { get; set; }
    private bool IsEditing { get; set; }
    private Guid CurrentWorkflowId { get; set; }

    // Workflow
    private WorkflowCreateDto CurrentWorkflow { get; set; } = new();
    private WorkflowUpdateDto EditingWorkflow { get; set; } = new();
    private ValidationHelper WorkflowValidation { get; } = new();

    // WorkflowDefinition
    private IReadOnlyList<LookupDto<Guid>> WorkflowDefinitionsCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<LookupDto<Guid>> SelectedWorkflowDefinition { get; set; } = new();

    // WorkflowTemplate
    private WorkflowTemplateWithNavigationPropertiesDto? CurrentWorkflowTemplateNav { get; set; }
    private WorkflowTemplateDto? CurrentWorkflowTemplate => CurrentWorkflowTemplateNav?.WorkflowTemplate;
    private WorkflowTemplateCreateDto NewWorkflowTemplate { get; set; } = new();
    private WorkflowTemplateUpdateDto EditingWorkflowTemplate { get; set; } = new();
    private ValidationHelper WorkflowTemplateValidation { get; } = new();
    private OutputFormat? NewWorkflowTemplateOutputFormat { get; set; }
    private SignMode? NewWorkflowTemplateSignMode { get; set; }
    private OutputFormat? EditingWorkflowTemplateOutputFormat { get; set; }
    private SignMode? EditingWorkflowTemplateSignMode { get; set; }

    // WorkflowStepTemplates
    private List<WorkflowStepTemplateWithNavigationPropertiesDto> WorkflowStepTemplatesNavList { get; set; } = new();
    private List<WorkflowStepTemplateDto> WorkflowStepTemplatesList => WorkflowStepTemplatesNavList.Select(x => x.WorkflowStepTemplate).ToList();
    private WorkflowStepTemplateCreateDto NewStepTemplate { get; set; } = new();
    private WorkflowStepTemplateUpdateDto EditingStepTemplate { get; set; } = new();
    private ValidationHelper StepTemplateCreateValidation { get; } = new();
    private ValidationHelper StepTemplateEditValidation { get; } = new();
    private Modal CreateStepTemplateModal { get; set; } = new();
    private Modal EditStepTemplateModal { get; set; } = new();
    private Guid EditingStepTemplateId { get; set; }
    private WorkflowStepType NewStepTemplateType { get; set; } = WorkflowStepType.PROCESS;
    private WorkflowStepType EditingStepTemplateType { get; set; } = WorkflowStepType.PROCESS;

    // WorkflowStepAssignments
    private List<WorkflowStepAssignmentWithNavigationPropertiesDto> WorkflowStepAssignmentsList { get; set; } = new();
    private WorkflowStepAssignmentCreateDto NewStepAssignment { get; set; } = new();
    private WorkflowStepAssignmentUpdateDto EditingStepAssignment { get; set; } = new();
    private ValidationHelper StepAssignmentCreateValidation { get; } = new();
    private ValidationHelper StepAssignmentEditValidation { get; } = new();
    private Modal CreateStepAssignmentModal { get; set; } = new();
    private Modal EditStepAssignmentModal { get; set; } = new();
    private Guid EditingStepAssignmentId { get; set; }

    // Lookup collections for Select2
    private IReadOnlyList<LookupDto<Guid>> WorkflowStepTemplatesCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> IdentityUsersCollection { get; set; } = new List<LookupDto<Guid>>();

    // Selected values for Select2
    private List<LookupDto<Guid>> SelectedNewStepTemplate { get; set; } = new();
    private List<LookupDto<Guid>> SelectedNewIdentityUser { get; set; } = new();
    private List<LookupDto<Guid>> SelectedEditStepTemplate { get; set; } = new();
    private List<LookupDto<Guid>> SelectedEditIdentityUser { get; set; } = new();

    // File upload state for WordTemplatePath
    private FilePicker WordTemplateFilePicker { get; set; } = new();
    private IFileEntry? SelectedWordTemplateFile { get; set; }
    private int WordTemplateFilePickerProgress { get; set; }
    private bool IsUploadingWordTemplate { get; set; }
    private string? UploadedWordTemplatePath { get; set; }
    private string? UploadedWordTemplateFileName { get; set; }

    // PDF viewer state for converted Word template
    private PdfViewer? WordTemplatePdfViewerRef { get; set; }
    private string? WordTemplatePdfUrl { get; set; }
    private string? UploadedWordTemplatePdfPath { get; set; }
    private bool IsConvertingToPdf { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await GetWorkflowDefinitionCollectionLookupAsync();
        
        if (Id.HasValue)
        {
            await LoadWorkflowAsync(Id.Value);
        }
        else
        {
            // New workflow
            CurrentWorkflow = new WorkflowCreateDto
            {
                IsActive = true
            };
            IsWorkflowSaved = false;
            IsTemplateSaved = false;
            HasWorkflowSteps = false;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SetBreadcrumbItemsAsync();
            await SetToolbarItemsAsync();
            await InvokeAsync(StateHasChanged);
        }
    }

    protected virtual ValueTask SetBreadcrumbItemsAsync()
    {
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["Workflows"], "/workflow-lists"));
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["WorkflowDetail"]));
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["Back"], () => { NavigationManager.NavigateTo("/workflow-lists"); return Task.CompletedTask; }, IconName.ArrowLeft);
        return ValueTask.CompletedTask;
    }

    private async Task LoadWorkflowAsync(Guid id)
    {
        try
        {
            var workflow = await WorkflowsAppService.GetAsync(id);
            CurrentWorkflowId = id;
            CurrentWorkflow = new WorkflowCreateDto
            {
                Code = workflow.Code,
                Name = workflow.Name,
                Description = workflow.Description,
                IsActive = workflow.IsActive,
                WorkflowDefinitionId = workflow.WorkflowDefinitionId
            };

            EditingWorkflow = ObjectMapper.Map<WorkflowDto, WorkflowUpdateDto>(workflow);
            
            // Set selected workflow definition
            await GetWorkflowDefinitionCollectionLookupAsync();
            var workflowDef = WorkflowDefinitionsCollection.FirstOrDefault(x => x.Id == workflow.WorkflowDefinitionId);
            if (workflowDef != null)
            {
                SelectedWorkflowDefinition = new List<LookupDto<Guid>> { workflowDef };
            }

            IsWorkflowSaved = true;
            IsTemplateSaved = false;
            HasWorkflowSteps = false;
            
            // Load template
            await LoadWorkflowTemplateAsync();
            // IsTemplateSaved is set in LoadWorkflowTemplateAsync
            
            // Load steps
            await LoadWorkflowStepTemplatesAsync();
            HasWorkflowSteps = WorkflowStepTemplatesNavList.Any();
            
            // Load assignments
            await LoadWorkflowStepAssignmentsAsync();
            HasWorkflowStepAssignments = WorkflowStepAssignmentsList.Any();
            
            // Trigger UI update to reflect step states
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private bool ValidateWorkflow()
    {
        WorkflowValidation.Reset();
        
        WorkflowValidation.ValidateRequiredString("Code", CurrentWorkflow.Code, "CodeRequired", () => L["The {0} field is required.", L["Code"]]);
        WorkflowValidation.ValidateRequiredString("Name", CurrentWorkflow.Name, "NameRequired", () => L["The {0} field is required.", L["Name"]]);
        WorkflowValidation.ValidateRequiredGuid("WorkflowDefinitionId", SelectedWorkflowDefinition?.FirstOrDefault()?.Id ?? default, "WorkflowDefinitionRequired", () => L["The {0} field is required.", L["WorkflowDefinition"]]);
        
        return WorkflowValidation.IsValid;
    }

    private async Task SaveWorkflowAsync()
    {
        try
        {
            if (!ValidateWorkflow())
            {
                await InvokeAsync(StateHasChanged);
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            CurrentWorkflow.WorkflowDefinitionId = SelectedWorkflowDefinition?.FirstOrDefault()?.Id ?? default;

            var created = await WorkflowsAppService.CreateAsync(CurrentWorkflow);
            // CurrentWorkflowId = created.Id;

            NavigationManager.NavigateTo($"/workflow-detail/{created.Id}", forceLoad: true);

            // IsWorkflowSaved = true;
            
            // Logger?.LogInformation("[SaveWorkflowAsync] Workflow saved successfully. CurrentWorkflowId = {CurrentWorkflowId}, IsWorkflowSaved = {IsWorkflowSaved}", 
            //     CurrentWorkflowId, IsWorkflowSaved);
            
            // await InvokeAsync(StateHasChanged);
            
            // await Task.Delay(50);
            
            // await LoadWorkflowTemplateAsync();
            // await LoadWorkflowStepTemplatesAsync();
            // HasWorkflowSteps = WorkflowStepTemplatesNavList.Any();
            // await LoadWorkflowStepAssignmentsAsync();
            
            // Logger?.LogInformation("[SaveWorkflowAsync] After loading data: CurrentWorkflowId = {CurrentWorkflowId}, CurrentWorkflow != null = {CurrentWorkflowNotNull}, CurrentWorkflowTemplate != null = {TemplateNotNull}", 
            //     CurrentWorkflowId, CurrentWorkflow != null, CurrentWorkflowTemplate != null);
            
            // await InvokeAsync(StateHasChanged);
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

    private async Task EnableEditAsync()
    {
        try
        {
            var workflow = await WorkflowsAppService.GetAsync(CurrentWorkflowId);
            EditingWorkflow = ObjectMapper.Map<WorkflowDto, WorkflowUpdateDto>(workflow);
            IsEditing = true;
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private bool ValidateWorkflowEdit()
    {
        WorkflowValidation.Reset();
        
        WorkflowValidation.ValidateRequiredString("Code", EditingWorkflow.Code, "CodeRequired", () => L["The {0} field is required.", L["Code"]]);
        WorkflowValidation.ValidateRequiredString("Name", EditingWorkflow.Name, "NameRequired", () => L["The {0} field is required.", L["Name"]]);
        WorkflowValidation.ValidateRequiredGuid("WorkflowDefinitionId", SelectedWorkflowDefinition?.FirstOrDefault()?.Id ?? default, "WorkflowDefinitionRequired", () => L["The {0} field is required.", L["WorkflowDefinition"]]);
        
        return WorkflowValidation.IsValid;
    }

    private async Task UpdateWorkflowAsync()
    {
        try
        {
            SyncEditingWorkflowFromCurrentWorkflow();

            if (!ValidateWorkflowEdit())
            {
                await InvokeAsync(StateHasChanged);
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            EditingWorkflow.WorkflowDefinitionId = SelectedWorkflowDefinition?.FirstOrDefault()?.Id ?? default;

            await WorkflowsAppService.UpdateAsync(CurrentWorkflowId, EditingWorkflow);
            await LoadWorkflowAsync(CurrentWorkflowId);
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

    private void SyncEditingWorkflowFromCurrentWorkflow()
    {
        EditingWorkflow.Code = CurrentWorkflow.Code;
        EditingWorkflow.Name = CurrentWorkflow.Name;
        EditingWorkflow.Description = CurrentWorkflow.Description;
        EditingWorkflow.IsActive = CurrentWorkflow.IsActive;
        EditingWorkflow.WorkflowDefinitionId = SelectedWorkflowDefinition?.FirstOrDefault()?.Id
                                              ?? CurrentWorkflow.WorkflowDefinitionId;
    }

    private async Task CancelEditAsync()
    {
        IsEditing = false;
        await LoadWorkflowAsync(CurrentWorkflowId);
    }

    private Task CancelAsync()
    {
        NavigationManager.NavigateTo("/workflow-lists");
        return Task.CompletedTask;
    }

    private async Task OnSelectedStepChanged(string name)
    {
        SelectedStep = name;
        await InvokeAsync(StateHasChanged);
    }

    private bool NavigationAllowed(StepNavigationContext context)
    {
        // Allow all navigation (clicking on any step) - validation will be done in NextStepAsync
        Logger?.LogInformation("[NavigationAllowed] Allowing navigation: CurrentStepIndex={CurrentStepIndex}, NextStepIndex={NextStepIndex}", 
            context.CurrentStepIndex, context.NextStepIndex);
        return true;
    }

    private async Task NextStepAsync()
    {
        if (StepsRef == null)
        {
            Logger?.LogWarning("[NextStepAsync] StepsRef is null, cannot navigate");
            return;
        }

        Logger?.LogInformation("[NextStepAsync] Before navigation: SelectedStep = {SelectedStep}, CurrentWorkflowId = {CurrentWorkflowId}, CurrentWorkflow != null = {CurrentWorkflowNotNull}, CurrentWorkflowTemplate != null = {TemplateNotNull}, WorkflowStepTemplatesList.Count = {StepTemplatesCount}", 
            SelectedStep, CurrentWorkflowId, CurrentWorkflow != null, CurrentWorkflowTemplate != null, WorkflowStepTemplatesList?.Count ?? 0);

        // Step 1 -> Step 2: Check if workflow is saved
        if (SelectedStep == "step1")
        {
            var canNavigate = CurrentWorkflow != null && CurrentWorkflowId != Guid.Empty;
            if (!canNavigate)
            {
                await UiMessageService.Warn(L["WorkflowDetail:PleaseCreateWorkflow"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                Logger?.LogInformation("[NextStepAsync] Cannot navigate from Step 1 to Step 2: Workflow not saved");
                return;
            }
        }
        
        // Step 2 -> Step 3: Check if template is saved
        if (SelectedStep == "step2")
        {
            var templateNotNull = CurrentWorkflowTemplate != null;
            var templateIdNotEmpty = CurrentWorkflowTemplate?.Id != Guid.Empty;
            var canNavigate = templateNotNull && templateIdNotEmpty == true;
            
            if (!canNavigate)
            {
                await UiMessageService.Warn(L["WorkflowDetail:PleaseCreateTemplate"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                Logger?.LogInformation("[NextStepAsync] Cannot navigate from Step 2 to Step 3: Template not saved");
                return;
            }
        }
        
        // Step 3 -> Step 4: Check if step templates exist
        if (SelectedStep == "step3")
        {
            var listNotNull = WorkflowStepTemplatesList != null;
            var listCount = WorkflowStepTemplatesList?.Count ?? 0;
            var canNavigate = listNotNull && listCount > 0;
            
            if (!canNavigate)
            {
                await UiMessageService.Warn(L["WorkflowDetail:PleaseCreateStep"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                Logger?.LogInformation("[NextStepAsync] Cannot navigate from Step 3 to Step 4: No step templates");
                return;
            }
        }

        // All conditions met, proceed with navigation
        await InvokeAsync(StateHasChanged);
        await Task.Delay(10);
        await StepsRef.NextStep();
        
        Logger?.LogInformation("[NextStepAsync] After navigation: SelectedStep = {SelectedStep}", SelectedStep);
        
        await InvokeAsync(StateHasChanged);
    }

    private async Task PreviousStepAsync()
    {
        if (StepsRef != null)
        {
            await StepsRef.PreviousStep();
        }
    }

    private async Task OnWorkflowDefinitionChanged()
    {
        CurrentWorkflow.WorkflowDefinitionId = SelectedWorkflowDefinition?.FirstOrDefault()?.Id ?? CurrentWorkflow.WorkflowDefinitionId;
        await InvokeAsync(StateHasChanged);
    }

    // WorkflowTemplate methods
    private async Task LoadWorkflowTemplateAsync()
    {
        if (CurrentWorkflowId == Guid.Empty) 
        {
            IsTemplateSaved = false;
            return;
        }

        try
        {
            var result = await WorkflowTemplatesAppService.GetListAsync(new GetWorkflowTemplatesInput
            {
                WorkflowId = CurrentWorkflowId,
                MaxResultCount = 1,
                SkipCount = 0
            });

            CurrentWorkflowTemplateNav = result.Items?.FirstOrDefault();
            
            var hasTemplate = CurrentWorkflowTemplateNav != null && CurrentWorkflowTemplateNav.WorkflowTemplate != null;
            IsTemplateSaved = hasTemplate;
            
            if (hasTemplate)
            {
                var template = CurrentWorkflowTemplateNav!.WorkflowTemplate;
                EditingWorkflowTemplate = ObjectMapper.Map<WorkflowTemplateDto, WorkflowTemplateUpdateDto>(template);
                if (Enum.TryParse<OutputFormat>(template.OutputFormat, out var outputFormat))
                {
                    EditingWorkflowTemplateOutputFormat = outputFormat;
                }
                if (Enum.TryParse<SignMode>(template.SignMode, out var signMode))
                {
                    EditingWorkflowTemplateSignMode = signMode;
                }
                
                // Load uploaded file info
                if (!string.IsNullOrWhiteSpace(template.WordTemplatePath))
                {
                    UploadedWordTemplatePath = template.WordTemplatePath;
                    UploadedWordTemplateFileName = Path.GetFileName(template.WordTemplatePath);
                }
                else
                {
                    UploadedWordTemplatePath = null;
                    UploadedWordTemplateFileName = null;
                }
                
                // Load converted PDF from PdfTemplatePath column
                if (!string.IsNullOrWhiteSpace(template.PdfTemplatePath))
                {
                    await LoadWordTemplatePdfAsync(template.PdfTemplatePath);
                }
                else
                {
                    UploadedWordTemplatePdfPath = null;
                    WordTemplatePdfUrl = null;
                }
                
                await InvokeAsync(StateHasChanged);
            }
            else
            {
                IsTemplateSaved = false;
                CurrentWorkflowTemplateNav = null;
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
            IsTemplateSaved = false;
            CurrentWorkflowTemplateNav = null;
        }
    }

    private bool ValidateWorkflowTemplateCreate()
    {
        WorkflowTemplateValidation.Reset();
        
        WorkflowTemplateValidation.ValidateRequiredString("Code", NewWorkflowTemplate.Code, "CodeRequired", () => L["The {0} field is required.", L["Code"]]);
        WorkflowTemplateValidation.ValidateRequiredString("Name", NewWorkflowTemplate.Name, "NameRequired", () => L["The {0} field is required.", L["Name"]]);
        WorkflowTemplateValidation.ValidateCustom("OutputFormat", NewWorkflowTemplateOutputFormat.HasValue, "OutputFormatRequired", () => L["The {0} field is required.", L["OutputFormat"]]);
        WorkflowTemplateValidation.ValidateCustom("SignMode", NewWorkflowTemplateSignMode.HasValue, "SignModeRequired", () => L["The {0} field is required.", L["SignMode"]]);
        
        return WorkflowTemplateValidation.IsValid;
    }

    private async Task CreateWorkflowTemplateAsync()
    {
        try
        {
            if (!ValidateWorkflowTemplateCreate())
            {
                await InvokeAsync(StateHasChanged);
                return;
            }

            if (CurrentWorkflowId == Guid.Empty)
            {
                await UiMessageService.Warn(L["WorkflowDetail:WorkflowNotSaved"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            
            NewWorkflowTemplate.WorkflowId = CurrentWorkflowId;
            NewWorkflowTemplate.OutputFormat = NewWorkflowTemplateOutputFormat?.ToString();
            NewWorkflowTemplate.SignMode = NewWorkflowTemplateSignMode?.ToString();
            NewWorkflowTemplate.WordTemplatePath = UploadedWordTemplatePath;
            NewWorkflowTemplate.PdfTemplatePath = UploadedWordTemplatePdfPath;

            await WorkflowTemplatesAppService.CreateAsync(NewWorkflowTemplate);
            NewWorkflowTemplate = new WorkflowTemplateCreateDto();
            NewWorkflowTemplateOutputFormat = null;
            NewWorkflowTemplateSignMode = null;
            
            // Reload template first
            await LoadWorkflowTemplateAsync();
            
            // Only load steps and assignments if template was successfully loaded
            if (IsTemplateSaved && CurrentWorkflowId != Guid.Empty)
            {
                try
                {
                    await LoadWorkflowStepTemplatesAsync();
                }
                catch (Exception ex)
                {
                    // Log but don't block UI - steps might not exist yet
                    await HandleErrorAsync(ex);
                }
                
                try
                {
                    await LoadWorkflowStepAssignmentsAsync();
                }
                catch (Exception ex)
                {
                    // Log but don't block UI - assignments might not exist yet
                    await HandleErrorAsync(ex);
                }
            }
            
            // Move to next step
            if (StepsRef != null && IsTemplateSaved)
            {
                await StepsRef.NextStep();
            }
            
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

    private bool ValidateWorkflowTemplateEdit()
    {
        WorkflowTemplateValidation.Reset();
        
        WorkflowTemplateValidation.ValidateRequiredString("Code", EditingWorkflowTemplate.Code, "CodeRequired", () => L["The {0} field is required.", L["Code"]]);
        WorkflowTemplateValidation.ValidateRequiredString("Name", EditingWorkflowTemplate.Name, "NameRequired", () => L["The {0} field is required.", L["Name"]]);
        WorkflowTemplateValidation.ValidateCustom("OutputFormat", EditingWorkflowTemplateOutputFormat.HasValue, "OutputFormatRequired", () => L["The {0} field is required.", L["OutputFormat"]]);
        WorkflowTemplateValidation.ValidateCustom("SignMode", EditingWorkflowTemplateSignMode.HasValue, "SignModeRequired", () => L["The {0} field is required.", L["SignMode"]]);
        
        return WorkflowTemplateValidation.IsValid;
    }

    private async Task UpdateWorkflowTemplateAsync()
    {
        try
        {
            if (!ValidateWorkflowTemplateEdit())
            {
                await InvokeAsync(StateHasChanged);
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);            
            EditingWorkflowTemplate.WorkflowId = CurrentWorkflowId;
            EditingWorkflowTemplate.OutputFormat = EditingWorkflowTemplateOutputFormat?.ToString();
            EditingWorkflowTemplate.SignMode = EditingWorkflowTemplateSignMode?.ToString();
            EditingWorkflowTemplate.WordTemplatePath = UploadedWordTemplatePath;
            EditingWorkflowTemplate.PdfTemplatePath = UploadedWordTemplatePdfPath;

            await WorkflowTemplatesAppService.UpdateAsync(CurrentWorkflowTemplate!.Id, EditingWorkflowTemplate);
            await LoadWorkflowTemplateAsync();
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

    private async Task DeleteWorkflowTemplateAsync()
    {
        try
        {
            if (!await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value))
            {
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);            
            await WorkflowTemplatesAppService.DeleteAsync(CurrentWorkflowTemplate!.Id);
            CurrentWorkflowTemplateNav = null;
            IsTemplateSaved = false;
            HasWorkflowSteps = false;
            WorkflowStepTemplatesNavList = new List<WorkflowStepTemplateWithNavigationPropertiesDto>();
            WorkflowStepAssignmentsList = new List<WorkflowStepAssignmentWithNavigationPropertiesDto>();
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

    // WorkflowStepTemplate methods
    private async Task LoadWorkflowStepTemplatesAsync()
    {
        // Only load if template is saved
        if (CurrentWorkflowTemplate == null || CurrentWorkflowTemplate.Id == Guid.Empty)
        {
            WorkflowStepTemplatesNavList = new List<WorkflowStepTemplateWithNavigationPropertiesDto>();
            HasWorkflowSteps = false;
            return;
        }

        try
        {
            var result = await WorkflowStepTemplatesAppService.GetListAsync(new GetWorkflowStepTemplatesInput
            {
                WorkflowTemplateId = CurrentWorkflowTemplate.Id,
                MaxResultCount = 200,
                SkipCount = 0,
                Sorting = "WorkflowStepTemplate.Order ASC"
            });

            WorkflowStepTemplatesNavList = result.Items?.ToList() ?? new List<WorkflowStepTemplateWithNavigationPropertiesDto>();
            HasWorkflowSteps = WorkflowStepTemplatesNavList.Any();
            
            // Only load lookup if we have items
            if (WorkflowStepTemplatesNavList.Any())
            {
                await LoadWorkflowStepTemplateLookupAsync();
            }
        }
        catch (Exception ex)
        {
            // Silently handle - steps might not exist yet
            WorkflowStepTemplatesNavList = new List<WorkflowStepTemplateWithNavigationPropertiesDto>();
            // Only show error if it's not a "not found" type error
            if (!ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                await HandleErrorAsync(ex);
            }
        }
    }

    private async Task OpenCreateStepTemplateModalAsync()
    {
        if (CurrentWorkflowTemplate == null || CurrentWorkflowTemplate.Id == Guid.Empty)
        {
            await UiMessageService.Warn(L["WorkflowDetail:SaveTemplateFirst"],
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            return;
        }

        NewStepTemplate = new WorkflowStepTemplateCreateDto
        {
            WorkflowTemplateId = CurrentWorkflowTemplate.Id,
            Order = WorkflowStepTemplatesList.Any() ? WorkflowStepTemplatesList.Max(x => x.Order) + 1 : 1,
            IsActive = true,
            Type = WorkflowStepType.PROCESS.ToString()
        };
        NewStepTemplateType = WorkflowStepType.PROCESS;
        StepTemplateCreateValidation.Reset();
        await CreateStepTemplateModal.Show();
    }

    private async Task CloseCreateStepTemplateModalAsync()
    {
        NewStepTemplate = new WorkflowStepTemplateCreateDto();
        StepTemplateCreateValidation.Reset();
        await CreateStepTemplateModal.Hide();
    }

    private bool ValidateStepTemplateCreate()
    {
        StepTemplateCreateValidation.Reset();
        
        StepTemplateCreateValidation.ValidateRequiredString("Name", NewStepTemplate.Name, "NameRequired", () => L["The {0} field is required.", L["Name"]]);
        StepTemplateCreateValidation.ValidateRequiredGuid("WorkflowTemplateId", NewStepTemplate.WorkflowTemplateId, "WorkflowTemplateRequired", () => L["The {0} field is required.", L["WorkflowTemplate"]]);
        
        return StepTemplateCreateValidation.IsValid;
    }

    private async Task CreateStepTemplateAsync()
    {
        try
        {
            if (!ValidateStepTemplateCreate())
            {
                await InvokeAsync(StateHasChanged);
                return;
            }
            
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            NewStepTemplate.Type = NewStepTemplateType.ToString();
            await WorkflowStepTemplatesAppService.CreateAsync(NewStepTemplate);
            await LoadWorkflowStepTemplatesAsync();
            HasWorkflowSteps = WorkflowStepTemplatesNavList.Any();
            await CloseCreateStepTemplateModalAsync();
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

    private async Task OpenEditStepTemplateModalAsync(WorkflowStepTemplateDto input)
    {
        EditingStepTemplateId = input.Id;
        EditingStepTemplate = ObjectMapper.Map<WorkflowStepTemplateDto, WorkflowStepTemplateUpdateDto>(input);
        if (Enum.TryParse<WorkflowStepType>(input.Type, out var type))
        {
            EditingStepTemplateType = type;
        }
        StepTemplateEditValidation.Reset();
        await EditStepTemplateModal.Show();
    }

    private async Task CloseEditStepTemplateModalAsync()
    {
        StepTemplateEditValidation.Reset();
        await EditStepTemplateModal.Hide();
    }

    private bool ValidateStepTemplateEdit()
    {
        StepTemplateEditValidation.Reset();
        
        StepTemplateEditValidation.ValidateRequiredString("Name", EditingStepTemplate.Name, "NameRequired", () => L["The {0} field is required.", L["Name"]]);
        
        return StepTemplateEditValidation.IsValid;
    }

    private async Task UpdateStepTemplateAsync()
    {
        try
        {
            if (!ValidateStepTemplateEdit())
            {
                await InvokeAsync(StateHasChanged);
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            EditingStepTemplate.Type = EditingStepTemplateType.ToString();
            await WorkflowStepTemplatesAppService.UpdateAsync(EditingStepTemplateId, EditingStepTemplate);
            await LoadWorkflowStepTemplatesAsync();
            HasWorkflowSteps = WorkflowStepTemplatesNavList.Any();
            await CloseEditStepTemplateModalAsync();
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

    private async Task DeleteStepTemplateAsync(WorkflowStepTemplateDto input)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            await WorkflowStepTemplatesAppService.DeleteAsync(input.Id);
            await LoadWorkflowStepTemplatesAsync();
            HasWorkflowSteps = WorkflowStepTemplatesNavList.Any();
            await LoadWorkflowStepAssignmentsAsync();
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

    private async Task DeleteStepTemplateWithConfirmationAsync(WorkflowStepTemplateDto input)
    {
        if (await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value))
        {
            await DeleteStepTemplateAsync(input);
        }
    }

    // WorkflowStepAssignment methods
    private async Task LoadWorkflowStepAssignmentsAsync()
    {
        // Only load if we have step templates
        if (WorkflowStepTemplatesList == null || !WorkflowStepTemplatesList.Any())
        {
            WorkflowStepAssignmentsList = new List<WorkflowStepAssignmentWithNavigationPropertiesDto>();
            return;
        }

        try
        {
            // Load assignments for all steps in the current template
            var allAssignments = new List<WorkflowStepAssignmentWithNavigationPropertiesDto>();
            
            foreach (var step in WorkflowStepTemplatesList)
            {
                var result = await WorkflowStepAssignmentsAppService.GetListAsync(new GetWorkflowStepAssignmentsInput
                {
                    StepId = step.Id,
                    MaxResultCount = 200,
                    SkipCount = 0
                });
                
                if (result.Items != null)
                {
                    allAssignments.AddRange(result.Items);
                }
            }

            WorkflowStepAssignmentsList = allAssignments;
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
            WorkflowStepAssignmentsList = new List<WorkflowStepAssignmentWithNavigationPropertiesDto>();
        }
    }

    private async Task OpenCreateStepAssignmentModalAsync()
    {
        if (WorkflowStepTemplatesList == null || !WorkflowStepTemplatesList.Any())
        {
            await UiMessageService.Warn(L["WorkflowDetail:PleaseCreateStep"],
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            return;
        }

        NewStepAssignment = new WorkflowStepAssignmentCreateDto
        {
            IsActive = true
        };
        SelectedNewStepTemplate = new List<LookupDto<Guid>>();
        SelectedNewIdentityUser = new List<LookupDto<Guid>>();
        await LoadWorkflowStepTemplateLookupAsync();
        await LoadIdentityUserLookupAsync();
        StepAssignmentCreateValidation.Reset();
        await CreateStepAssignmentModal.Show();
    }

    private async Task CloseCreateStepAssignmentModalAsync()
    {
        NewStepAssignment = new WorkflowStepAssignmentCreateDto();
        StepAssignmentCreateValidation.Reset();
        await CreateStepAssignmentModal.Hide();
    }

    private bool ValidateStepAssignmentCreate()
    {
        StepAssignmentCreateValidation.Reset();
        
        StepAssignmentCreateValidation.ValidateRequiredGuid("StepId", SelectedNewStepTemplate?.FirstOrDefault()?.Id ?? default, "StepRequired", () => L["The {0} field is required.", L["Step"]]);
        
        return StepAssignmentCreateValidation.IsValid;
    }

    private async Task CreateStepAssignmentAsync()
    {
        try
        {
            if (!ValidateStepAssignmentCreate())
            {
                await InvokeAsync(StateHasChanged);
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            NewStepAssignment.StepId = SelectedNewStepTemplate?.FirstOrDefault()?.Id;
            NewStepAssignment.DefaultUserId = SelectedNewIdentityUser?.FirstOrDefault()?.Id;
            await WorkflowStepAssignmentsAppService.CreateAsync(NewStepAssignment);
            await LoadWorkflowStepAssignmentsAsync();
            await CloseCreateStepAssignmentModalAsync();
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

    private async Task OpenEditStepAssignmentModalAsync(WorkflowStepAssignmentWithNavigationPropertiesDto input)
    {
        EditingStepAssignmentId = input.WorkflowStepAssignment.Id;
        EditingStepAssignment = ObjectMapper.Map<WorkflowStepAssignmentDto, WorkflowStepAssignmentUpdateDto>(input.WorkflowStepAssignment);
        
        await LoadWorkflowStepTemplateLookupAsync();
        await LoadIdentityUserLookupAsync();
        
        if (input.WorkflowStepAssignment.StepId.HasValue)
        {
            var step = WorkflowStepTemplatesCollection.FirstOrDefault(x => x.Id == input.WorkflowStepAssignment.StepId.Value);
            if (step != null)
            {
                SelectedEditStepTemplate = new List<LookupDto<Guid>> { step };
            }
        }
        
        if (input.WorkflowStepAssignment.DefaultUserId.HasValue)
        {
            var user = IdentityUsersCollection.FirstOrDefault(x => x.Id == input.WorkflowStepAssignment.DefaultUserId.Value);
            if (user != null)
            {
                SelectedEditIdentityUser = new List<LookupDto<Guid>> { user };
            }
        }
        
        StepAssignmentEditValidation.Reset();
        await EditStepAssignmentModal.Show();
    }

    private async Task CloseEditStepAssignmentModalAsync()
    {
        StepAssignmentEditValidation.Reset();
        await EditStepAssignmentModal.Hide();
    }

    private bool ValidateStepAssignmentEdit()
    {
        StepAssignmentEditValidation.Reset();
        
        StepAssignmentEditValidation.ValidateRequiredGuid("StepId", SelectedEditStepTemplate?.FirstOrDefault()?.Id ?? default, "StepRequired", () => L["The {0} field is required.", L["Step"]]);
        
        return StepAssignmentEditValidation.IsValid;
    }

    private async Task UpdateStepAssignmentAsync()
    {
        try
        {
            if (!ValidateStepAssignmentEdit())
            {
                await InvokeAsync(StateHasChanged);
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);            
            EditingStepAssignment.StepId = SelectedEditStepTemplate?.FirstOrDefault()?.Id;
            EditingStepAssignment.DefaultUserId = SelectedEditIdentityUser?.FirstOrDefault()?.Id;
            await WorkflowStepAssignmentsAppService.UpdateAsync(EditingStepAssignmentId, EditingStepAssignment);
            await LoadWorkflowStepAssignmentsAsync();
            await CloseEditStepAssignmentModalAsync();
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

    private async Task DeleteStepAssignmentAsync(WorkflowStepAssignmentWithNavigationPropertiesDto input)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);            
            await WorkflowStepAssignmentsAppService.DeleteAsync(input.WorkflowStepAssignment.Id);
            await LoadWorkflowStepAssignmentsAsync();
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

    private async Task DeleteStepAssignmentWithConfirmationAsync(WorkflowStepAssignmentWithNavigationPropertiesDto input)
    {
        if (await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value))
        {
            await DeleteStepAssignmentAsync(input);
        }
    }

    // Lookup methods
    private async Task GetWorkflowDefinitionCollectionLookupAsync(string? newValue = null)
    {
        WorkflowDefinitionsCollection = (await WorkflowsAppService.GetWorkflowDefinitionLookupAsync(new LookupRequestDto { Filter = newValue })).Items
            ?? new List<LookupDto<Guid>>();
    }

    private async Task<List<LookupDto<Guid>>> GetWorkflowDefinitionCollectionLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var result = await WorkflowsAppService.GetWorkflowDefinitionLookupAsync(new LookupRequestDto { Filter = filter });
        WorkflowDefinitionsCollection = result.Items ?? new List<LookupDto<Guid>>();
        return WorkflowDefinitionsCollection.ToList();
    }

    private async Task LoadWorkflowStepTemplateLookupAsync()
    {
        if (CurrentWorkflowTemplate == null || CurrentWorkflowTemplate.Id == Guid.Empty) return;
        
        var result = await WorkflowStepTemplatesAppService.GetListAsync(new GetWorkflowStepTemplatesInput
        {
            WorkflowTemplateId = CurrentWorkflowTemplate.Id,
            MaxResultCount = 200,
            SkipCount = 0
        });
        
        WorkflowStepTemplatesCollection = result.Items?.Select(x => new LookupDto<Guid> 
        { 
            Id = x.WorkflowStepTemplate.Id, 
            DisplayName = x.WorkflowStepTemplate.Name 
        }).ToList() ?? new List<LookupDto<Guid>>();
    }

    private async Task<List<LookupDto<Guid>>> GetWorkflowStepTemplateCollectionLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        if (CurrentWorkflowTemplate == null || CurrentWorkflowTemplate.Id == Guid.Empty)
        {
            return new List<LookupDto<Guid>>();
        }
        
        var result = await WorkflowStepTemplatesAppService.GetListAsync(new GetWorkflowStepTemplatesInput 
        { 
            WorkflowTemplateId = CurrentWorkflowTemplate.Id, 
            MaxResultCount = 200, 
            SkipCount = 0 
        });
        
        WorkflowStepTemplatesCollection = result.Items?.Select(x => new LookupDto<Guid> 
        { 
            Id = x.WorkflowStepTemplate.Id, 
            DisplayName = x.WorkflowStepTemplate.Name 
        }).ToList() ?? new List<LookupDto<Guid>>();
        
        return WorkflowStepTemplatesCollection.ToList();
    }


    private async Task LoadIdentityUserLookupAsync()
    {
        var result = await WorkflowStepAssignmentsAppService.GetIdentityUserLookupAsync(new LookupRequestDto
        {
            Filter = null,
            MaxResultCount = 200,
            SkipCount = 0
        });
        IdentityUsersCollection = result.Items;
    }

    private async Task<List<LookupDto<Guid>>> GetIdentityUserCollectionLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var result = await WorkflowStepAssignmentsAppService.GetIdentityUserLookupAsync(new LookupRequestDto { Filter = filter });
        IdentityUsersCollection = result.Items;
        return result.Items.ToList();
    }

    private async Task OnNewStepTemplateChanged()
    {
        NewStepAssignment.StepId = SelectedNewStepTemplate?.FirstOrDefault()?.Id;
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnNewIdentityUserChanged()
    {
        NewStepAssignment.DefaultUserId = SelectedNewIdentityUser?.FirstOrDefault()?.Id;
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnEditStepTemplateChanged()
    {
        EditingStepAssignment.StepId = SelectedEditStepTemplate?.FirstOrDefault()?.Id;
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnEditWorkflowTemplateChanged()
    {
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnEditIdentityUserChanged()
    {
        EditingStepAssignment.DefaultUserId = SelectedEditIdentityUser?.FirstOrDefault()?.Id;
        await InvokeAsync(StateHasChanged);
    }

    // Word Template File Upload Methods
    private async Task OnWordTemplateFileChanged(FileChangedEventArgs e)
    {
        if (e.Files != null && e.Files.Any())
        {
            var file = e.Files.First();
            await UploadWordTemplateAsync(file);
        }
    }

    private async Task UploadWordTemplateAsync(IFileEntry file)
    {
        try
        {
            // Validate file size (50MB max)
            if (file.Size > 52428800)
            {
                await UiMessageService.Error(L["FileSizeTooLarge", 50],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                await WordTemplateFilePicker.Clear();
                return;
            }

            // Validate file extension (only .docx, .doc, .pdf)
            var fileExtension = Path.GetExtension(file.Name).ToLowerInvariant();
            if (fileExtension != ".docx" && fileExtension != ".doc" && fileExtension != ".pdf")
            {
                await UiMessageService.Error(L["OnlyWordAndPdfFilesAllowed"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                await WordTemplateFilePicker.Clear();
                return;
            }

            var isWordFile = fileExtension == ".docx" || fileExtension == ".doc";
            var isPdfFile = fileExtension == ".pdf";

            IsUploadingWordTemplate = true;
            // Only show converting indicator for Word files
            IsConvertingToPdf = isWordFile;
            SelectedWordTemplateFile = file;
            WordTemplateFilePickerProgress = 0;
            await InvokeAsync(StateHasChanged);

            // Read file stream FIRST before any other async operations
            // to prevent SignalR pipe reader from being completed/timeout
            using var memoryStream = new MemoryStream();
            await using (var fileStream = file.OpenReadStream(long.MaxValue))
            {
                await fileStream.CopyToAsync(memoryStream);
            }
            memoryStream.Position = 0;

            var fileBytes = memoryStream.ToArray();

            // Delete old files if exists (safe to do after reading the new file)
            var oldFilePath = UploadedWordTemplatePath ?? CurrentWorkflowTemplate?.WordTemplatePath;
            if (!string.IsNullOrWhiteSpace(oldFilePath))
            {
                try
                {
                    await BlobContainer.DeleteAsync(oldFilePath);
                }
                catch
                {
                    // Ignore if file doesn't exist
                }
            }

            // Delete old PDF if exists
            if (!string.IsNullOrWhiteSpace(UploadedWordTemplatePdfPath))
            {
                try
                {
                    await BlobContainer.DeleteAsync(UploadedWordTemplatePdfPath);
                }
                catch
                {
                    // Ignore if file doesn't exist
                }
            }

            // Generate unique file name
            var fileName = $"{Guid.NewGuid()}_{file.Name}";
            var filePath = $"workflow-templates/{fileName}";

            // Upload original file to MinIO
            await BlobContainer.SaveAsync(filePath, fileBytes);
            UploadedWordTemplatePath = filePath;
            UploadedWordTemplateFileName = file.Name;
            WordTemplateFilePickerProgress = isPdfFile ? 80 : 50;
            await InvokeAsync(StateHasChanged);

            if (isPdfFile)
            {
                // PDF uploaded directly - use same file as PDF preview, no conversion needed
                UploadedWordTemplatePdfPath = filePath;
                var base64 = Convert.ToBase64String(fileBytes);
                WordTemplatePdfUrl = $"data:application/pdf;base64,{base64}";
                Logger?.LogInformation("PDF file uploaded directly to MinIO: {Path}", filePath);
            }
            else
            {
                // Word file - convert to PDF using LibreOffice
                byte[]? pdfBytes = null;
                string? tempDocxPath = null;
                try
                {
                    // Write Word file to temp location for conversion
                    tempDocxPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{fileExtension}");
                    await File.WriteAllBytesAsync(tempDocxPath, fileBytes);

                    Logger?.LogInformation("Converting Word to PDF: {FileName}", file.Name);
                    pdfBytes = await DocxToPdfConverter.ConvertFileAsync(tempDocxPath);
                    Logger?.LogInformation("Word to PDF conversion completed: {FileName}, PDF size: {Size} bytes", file.Name, pdfBytes.Length);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Failed to convert Word to PDF: {FileName}", file.Name);
                    // Show warning to user - file uploaded OK but PDF conversion failed
                    await UiMessageService.Warn(
                        L["WordToPdfConversionFailed"],
                        options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                }
                finally
                {
                    // Cleanup temp file
                    if (!string.IsNullOrEmpty(tempDocxPath) && File.Exists(tempDocxPath))
                    {
                        try { File.Delete(tempDocxPath); }
                        catch { /* ignore */ }
                    }
                }

                // Upload converted PDF to MinIO and show in PDF viewer
                if (pdfBytes != null && pdfBytes.Length > 0)
                {
                    var pdfFileName = $"{Path.GetFileNameWithoutExtension(fileName)}.pdf";
                    var pdfFilePath = $"workflow-templates/pdf/{pdfFileName}";
                    await BlobContainer.SaveAsync(pdfFilePath, pdfBytes);
                    UploadedWordTemplatePdfPath = pdfFilePath;

                    // Create data URL for PDF viewer
                    var base64 = Convert.ToBase64String(pdfBytes);
                    WordTemplatePdfUrl = $"data:application/pdf;base64,{base64}";

                    Logger?.LogInformation("PDF uploaded to MinIO: {PdfPath}", pdfFilePath);
                }
                else
                {
                    UploadedWordTemplatePdfPath = null;
                    WordTemplatePdfUrl = null;
                }
            }

            WordTemplateFilePickerProgress = 100;
            IsConvertingToPdf = false;

            await UiMessageService.Success(L["FileUploadedSuccessfully"],
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
            UploadedWordTemplatePath = null;
            UploadedWordTemplateFileName = null;
            UploadedWordTemplatePdfPath = null;
            WordTemplatePdfUrl = null;
            WordTemplateFilePickerProgress = 0;
        }
        finally
        {
            IsUploadingWordTemplate = false;
            IsConvertingToPdf = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>
    /// Load converted PDF from MinIO for the PDF viewer using the stored PdfTemplatePath.
    /// </summary>
    private async Task LoadWordTemplatePdfAsync(string pdfTemplatePath)
    {
        try
        {
            var pdfBytes = await DocumentPdfViewerAppService.GetWatermarkedPdfAsync(new HC.DocumentPdfViewer.GetWatermarkedPdfInput
            {
                BlobPath = pdfTemplatePath,
                WatermarkAction = "view"
            });
            if (pdfBytes != null && pdfBytes.Length > 0)
            {
                UploadedWordTemplatePdfPath = pdfTemplatePath;
                var base64 = Convert.ToBase64String(pdfBytes);
                WordTemplatePdfUrl = $"data:application/pdf;base64,{base64}";
                Logger?.LogInformation("PDF loaded for viewer: {PdfPath}", pdfTemplatePath);
            }
        }
        catch (Exception ex)
        {
            // PDF might not exist yet (old templates before this feature)
            Logger?.LogWarning(ex, "Could not load converted PDF from MinIO: {Path}", pdfTemplatePath);
            UploadedWordTemplatePdfPath = null;
            WordTemplatePdfUrl = null;
        }
    }

    private async Task DeleteWordTemplateAsync()
    {
        try
        {
            var filePathToDelete = UploadedWordTemplatePath ?? CurrentWorkflowTemplate?.WordTemplatePath;
            
            if (string.IsNullOrWhiteSpace(filePathToDelete))
            {
                return;
            }

            // Delete Word file from MinIO
            await BlobContainer.DeleteAsync(filePathToDelete);

            // Delete converted PDF from MinIO
            if (!string.IsNullOrWhiteSpace(UploadedWordTemplatePdfPath))
            {
                try
                {
                    await BlobContainer.DeleteAsync(UploadedWordTemplatePdfPath);
                }
                catch
                {
                    // Ignore if PDF doesn't exist
                }
            }

            UploadedWordTemplatePath = null;
            UploadedWordTemplateFileName = null;
            UploadedWordTemplatePdfPath = null;
            WordTemplatePdfUrl = null;
            await WordTemplateFilePicker.Clear();

            // Update template if exists
            if (CurrentWorkflowTemplate != null)
            {
                EditingWorkflowTemplate.WordTemplatePath = null;
                EditingWorkflowTemplate.PdfTemplatePath = null;
            }
            else
            {
                NewWorkflowTemplate.WordTemplatePath = null;
                NewWorkflowTemplate.PdfTemplatePath = null;
            }

            await UiMessageService.Success(L["FileDeletedSuccessfully"],
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private bool HasWordTemplateFile()
    {
        var hasUploaded = !string.IsNullOrWhiteSpace(UploadedWordTemplatePath);
        var hasExisting = !string.IsNullOrWhiteSpace(CurrentWorkflowTemplate?.WordTemplatePath);
        return hasUploaded || hasExisting;
    }

    private string GetWordTemplateFileName()
    {
        if (!string.IsNullOrWhiteSpace(UploadedWordTemplateFileName))
        {
            return UploadedWordTemplateFileName;
        }
        if (!string.IsNullOrWhiteSpace(CurrentWorkflowTemplate?.WordTemplatePath))
        {
            return Path.GetFileName(CurrentWorkflowTemplate.WordTemplatePath);
        }
        return string.Empty;
    }

    private string GetWordTemplatePath()
    {
        // Priority: UploadedWordTemplatePath (newly uploaded) > CurrentWorkflowTemplate.WordTemplatePath (saved)
        if (!string.IsNullOrWhiteSpace(UploadedWordTemplatePath))
        {
            return UploadedWordTemplatePath;
        }
        if (!string.IsNullOrWhiteSpace(CurrentWorkflowTemplate?.WordTemplatePath))
        {
            return CurrentWorkflowTemplate.WordTemplatePath;
        }
        return string.Empty;
    }



    public void Dispose()
    {
        // Cleanup if needed
    }
}
