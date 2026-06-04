using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Blazorise;
using Blazorise.RichTextEdit;
using HC.Documents;
using HC.DocumentFiles;
using HC.DocumentWorkflowInstances;
using HC.WorkflowStepAssignments;
using HC.Shared;
using HC.Blazor.Pages;
using HC.Blazor.Components.Select2;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Components.Messages;
using Volo.Abp.BlobStoring;
using Volo.Abp.Timing;

namespace HC.Blazor.Components.SubmitWorkflowModal;

/// <summary>
/// Reusable modal for submitting a document to a workflow (Submit for Signing).
/// Can be used from Documents page (with pre-selected document) or DocumentSigning page (select document).
/// </summary>
public partial class SubmitWorkflowModal
{
    [Parameter] public EventCallback OnSubmitted { get; set; }
    [Parameter] public EventCallback OnClosed { get; set; }

    [Inject] private IDocumentWorkflowInstancesAppService DocumentWorkflowInstancesAppService { get; set; } = default!;
    [Inject] private IWorkflowStepAssignmentsAppService WorkflowStepAssignmentsAppService { get; set; } = default!;
    [Inject] private IDocumentsAppService DocumentsAppService { get; set; } = default!;
    [Inject] private IDocumentFilesAppService DocumentFilesAppService { get; set; } = default!;
    [Inject] private IBlobContainer BlobContainer { get; set; } = default!;
    [Inject] private IMemoryCache __MemoryCache { get; set; } = default!;

    private bool IsLoadingWorkflowInfo { get; set; }

    private bool IsSubmitting { get; set; }

    private bool IsModalBusy => IsLoadingWorkflowInfo || IsSubmitting;
    [Inject] private IClock Clock { get; set; } = default!;

    private Modal ModalRef { get; set; } = new();
    private Guid? SelectedWorkflowId { get; set; }
    private IReadOnlyList<LookupDto<Guid>> WorkflowsCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<LookupDto<Guid>> SelectedWorkflow { get; set; } = new();
    private WorkflowSubmitInfoDto? WorkflowSubmitInfo { get; set; }
    private bool UseWorkflowTemplateFile { get; set; }
    private bool UseTemplateFile { get; set; } = true;

    private List<DocumentWithNavigationPropertiesDto> MyDocumentsList { get; set; } = new();
    private Guid? SelectedDocumentId { get; set; }
    private DocumentWithNavigationPropertiesDto? SelectedDocumentDto { get; set; }
    private DocumentWithNavigationPropertiesDto? PreSelectedDocument { get; set; }
    private int ModalResetKey { get; set; }

    private string? SigningContent { get; set; }
    private RichTextEdit? SigningContentEditorRef { get; set; }
    private bool IsSelectedDocumentWordFormat { get; set; }

    private bool RequireSigningContent =>
        (UseWorkflowTemplateFile && WorkflowSubmitInfo?.IsTemplateFileWordFormat == true)
        || (!UseWorkflowTemplateFile && SelectedDocumentId.HasValue && IsSelectedDocumentWordFormat);

    private FilePicker? WorkflowFilePicker { get; set; }
    private List<UploadedFileInfo> UploadedFiles { get; set; } = new();
    private Dictionary<Guid, Guid?> StepSignerSelections { get; set; } = new();
    private List<ViewStepScopeEditorState> ViewStepScopeEditors { get; set; } = new();
    private List<DepartmentTreeView> ViewStepDepartmentTreeViews { get; set; } = new();
    private List<DepartmentTreeView> AllViewStepDepartmentsFlat { get; set; } = new();
    private IReadOnlyList<LookupDto<Guid>> IdentityUsersCollection { get; set; } = new List<LookupDto<Guid>>();

    private sealed class ViewStepScopeEditorState
    {
        public Guid StepId { get; init; }
        public string StepName { get; init; } = string.Empty;
        public List<DepartmentTreeView> SelectedDepartments { get; set; } = new();
        public List<LookupDto<Guid>> SelectedUsers { get; set; } = new();
    }

    private class UploadedFileInfo
    {
        public Guid DocumentFileId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }

    /// <summary>
    /// Show the modal. Pass preSelectedDocument when opening from Documents page with a specific document.
    /// </summary>
    public async Task ShowAsync(DocumentWithNavigationPropertiesDto? preSelectedDocument = null)
    {
        IsSubmitting = false;
        IsLoadingWorkflowInfo = false;
        PreSelectedDocument = preSelectedDocument;
        SelectedDocumentId = preSelectedDocument?.Document?.Id;
        SelectedDocumentDto = preSelectedDocument;
        SelectedWorkflowId = null;
        SelectedWorkflow.Clear();
        WorkflowsCollection = new List<LookupDto<Guid>>();
        WorkflowSubmitInfo = null;
        UseTemplateFile = true;
        UseWorkflowTemplateFile = preSelectedDocument != null ? false : UseWorkflowTemplateFile;
        SigningContent = null;
        UploadedFiles.Clear();
        StepSignerSelections.Clear();
        ViewStepScopeEditors.Clear();
        ModalResetKey++;
        IsSelectedDocumentWordFormat = false;

        if (WorkflowFilePicker != null)
        {
            await WorkflowFilePicker.Clear();
        }

        if (preSelectedDocument != null)
        {
            MyDocumentsList = new List<DocumentWithNavigationPropertiesDto> { preSelectedDocument };
            try
            {
                IsSelectedDocumentWordFormat = await DocumentWorkflowInstancesAppService
                    .IsDocumentSourceFileWordFormatAsync(preSelectedDocument.Document.Id);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to check document source file format for {DocumentId}", preSelectedDocument.Document.Id);
            }
        }
        else
        {
            await LoadMyDocumentsAsync();
        }

        await InvokeAsync(ModalRef.Show);
        await InvokeAsync(StateHasChanged);
    }

    private async Task GetWorkflowCollectionLookupAsync(string? newValue = null)
    {
        try
        {
            var result = await DocumentWorkflowInstancesAppService.GetWorkflowLookupAsync(
                new LookupRequestDto { Filter = newValue, MaxResultCount = 20 });
            WorkflowsCollection = result.Items ?? new List<LookupDto<Guid>>();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading workflow lookup");
        }
    }

    private async Task<List<LookupDto<Guid>>> GetWorkflowCollectionLookupAsync(
        IReadOnlyList<LookupDto<Guid>> dbset,
        string filter,
        CancellationToken token)
    {
        var result = await DocumentWorkflowInstancesAppService.GetWorkflowLookupAsync(
            new LookupRequestDto { Filter = filter, MaxResultCount = 20 });
        WorkflowsCollection = result.Items ?? new List<LookupDto<Guid>>();
        return WorkflowsCollection.ToList();
    }

    private async Task OnWorkflowSelect2ChangedAsync()
    {
        var workflowId = SelectedWorkflow.FirstOrDefault()?.Id;
        await OnWorkflowSelectedAsync(workflowId == Guid.Empty ? null : workflowId);
    }

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

    private async Task OnDocumentSelectedAsync(Guid? documentId)
    {
        SelectedDocumentId = documentId;
        SelectedDocumentDto = documentId.HasValue
            ? MyDocumentsList.FirstOrDefault(d => d.Document.Id == documentId.Value)
            : null;
        IsSelectedDocumentWordFormat = false;
        if (documentId.HasValue)
        {
            try
            {
                IsSelectedDocumentWordFormat = await DocumentWorkflowInstancesAppService
                    .IsDocumentSourceFileWordFormatAsync(documentId.Value);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to check document source file format for {DocumentId}", documentId);
            }
        }
        await InvokeAsync(StateHasChanged);
    }

    private void OnUseWorkflowTemplateFileChanged(bool value)
    {
        UseWorkflowTemplateFile = value;
        if (value)
        {
            SelectedDocumentId = null;
            SelectedDocumentDto = null;
            IsSelectedDocumentWordFormat = false;
        }
    }

    private async Task OnWorkflowSelectedAsync(Guid? workflowId)
    {
        SelectedWorkflowId = workflowId;
        WorkflowSubmitInfo = null;

        if (workflowId.HasValue && workflowId.Value != Guid.Empty)
        {
            IsLoadingWorkflowInfo = true;
            await InvokeAsync(StateHasChanged);

            try
            {
                WorkflowSubmitInfo = await DocumentWorkflowInstancesAppService.GetWorkflowSubmitInfoAsync(workflowId.Value);
                StepSignerSelections.Clear();
                await LoadViewStepOrganizationUnitTreeAsync();
                await LoadIdentityUserLookupAsync();
                InitializeViewStepScopeEditors();
            }
            catch (Exception ex)
            {
                await HandleErrorAsync(ex);
            }
            finally
            {
                IsLoadingWorkflowInfo = false;
                await InvokeAsync(StateHasChanged);
            }
        }
        else
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task CloseModalAsync()
    {
        await InvokeAsync(ModalRef.Hide);
        await OnClosed.InvokeAsync();
    }

    private async Task OnFileUpload(FileUploadEventArgs e)
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
                UploadedAt = Clock.Now,
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

    private async Task ConfirmSubmitAsync()
    {
        try
        {
            if (!SelectedWorkflowId.HasValue || WorkflowSubmitInfo == null)
            {
                await UiMessageService.Error(L["PleaseSelectWorkflow"],
                    options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            if (!UseWorkflowTemplateFile && !SelectedDocumentId.HasValue)
            {
                await UiMessageService.Error(L["The {0} field is required.", L["Document"]],
                    options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            var firstBlockingStep = WorkflowSubmitInfo.Steps
                .Where(s => !s.IsViewStep)
                .OrderBy(s => s.Order)
                .FirstOrDefault();
            if (firstBlockingStep != null && !firstBlockingStep.CandidateUsers.Any())
            {
                await UiMessageService.Error(L["FirstStepMustHaveAssignedUsers"],
                    options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            if (!ValidateViewScopeSelections())
            {
                await UiMessageService.Error(L["ViewStepScopeRequired"],
                    options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            if (!ValidateSignerSelections())
            {
                await UiMessageService.Error(L["WorkflowSignerSelectionRequired"],
                    options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            var signingContent = SigningContent?.Trim();
            if (SigningContentEditorRef != null)
            {
                var editorHtml = await SigningContentEditorRef.GetHtmlAsync();
                if (!string.IsNullOrWhiteSpace(editorHtml))
                {
                    signingContent = editorHtml.Trim();
                }
            }

            if (RequireSigningContent && string.IsNullOrWhiteSpace(signingContent))
            {
                await UiMessageService.Error(L["The {0} field is required.", L["SigningContent"]],
                    options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            var confirmed = await UiMessageService.Confirm(L["ConfirmSubmitForSigning"]);
            if (!confirmed) return;

            IsSubmitting = true;
            await InvokeAsync(StateHasChanged);

            var input = new SubmitToWorkflowInput
            {
                DocumentId = UseWorkflowTemplateFile ? null : SelectedDocumentId,
                WorkflowId = SelectedWorkflowId.Value,
                UseWorkflowTemplateFile = UseWorkflowTemplateFile,
                UseTemplateFile = UseTemplateFile,
                SigningContent = signingContent,
                AttachedFileIds = UploadedFiles.Any()
                    ? UploadedFiles.Select(f => f.DocumentFileId).ToList()
                    : null,
                StepSignerSelections = BuildStepSignerSelections(),
                ViewStepScopeSelections = BuildViewStepScopeSelections()
            };

            await DocumentWorkflowInstancesAppService.SubmitToWorkflowAsync(input);

            await UiMessageService.Success(L["WorkflowSubmittedSuccessfully"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            await InvokeAsync(ModalRef.Hide);
            await OnSubmitted.InvokeAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsSubmitting = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private IEnumerable<WorkflowStepDetailDto> GetStepsToAssignAtSubmit()
    {
        if (WorkflowSubmitInfo == null)
        {
            return Enumerable.Empty<WorkflowStepDetailDto>();
        }

        return WorkflowSubmitInfo.Steps.OrderBy(s => s.Order).ToList();
    }

    private bool IsStepVisibleAtSubmit(WorkflowStepDetailDto step)
    {
        return GetStepsToAssignAtSubmit().Any(s => s.StepId == step.StepId);
    }

    private bool ValidateSignerSelections()
    {
        foreach (var step in GetStepsToAssignAtSubmit())
        {
            if (step.RequiresSignerSelection
                && (!StepSignerSelections.TryGetValue(step.StepId, out var selectedId) || !selectedId.HasValue))
            {
                return false;
            }
        }

        return true;
    }

    private List<WorkflowStepSignerSelectionDto> BuildStepSignerSelections()
    {
        return StepSignerSelections
            .Where(x => x.Value.HasValue)
            .Select(x => new WorkflowStepSignerSelectionDto
            {
                StepId = x.Key,
                SelectedUserId = x.Value!.Value
            })
            .ToList();
    }

    private static string GetCandidateDisplayLabel(WorkflowStepUserDto user)
    {
        var name = string.IsNullOrWhiteSpace(user.FullName) ? user.UserName : user.FullName;
        if (!string.IsNullOrWhiteSpace(user.OrganizationUnitName))
        {
            return $"{name} — {user.OrganizationUnitName}";
        }

        return name;
    }

    private void OnSignerSelectionChanged(Guid stepId, Guid? userId)
    {
        StepSignerSelections[stepId] = userId;
    }

    private async Task LoadViewStepOrganizationUnitTreeAsync()
    {
        var organizationUnits = await DocumentsAppService.GetOrganizationUnitTreeAsync();
        var departments = organizationUnits
            .Select(ou => new DepartmentTreeView
            {
                Id = ou.Id,
                ParentId = ou.ParentId?.ToString(),
                Code = ou.Code,
                Name = ou.DisplayName
            })
            .ToList();

        var departmentsDictionary = new Dictionary<string, List<DepartmentTreeView>>();
        foreach (var department in departments)
        {
            var parentId = department.ParentId ?? string.Empty;
            if (!departmentsDictionary.ContainsKey(parentId))
            {
                departmentsDictionary[parentId] = new List<DepartmentTreeView>();
            }

            departmentsDictionary[parentId].Add(department);
        }

        foreach (var department in departments)
        {
            var departmentId = department.Id.ToString();
            department.Children = departmentsDictionary.TryGetValue(departmentId, out var children)
                ? children
                : new List<DepartmentTreeView>();
        }

        ViewStepDepartmentTreeViews = departmentsDictionary.TryGetValue(string.Empty, out var roots)
            ? roots
            : new List<DepartmentTreeView>();
        AllViewStepDepartmentsFlat = FlattenViewStepDepartments(ViewStepDepartmentTreeViews);
    }

    private static List<DepartmentTreeView> FlattenViewStepDepartments(IEnumerable<DepartmentTreeView> nodes)
    {
        var result = new List<DepartmentTreeView>();
        foreach (var node in nodes)
        {
            result.Add(node);
            result.AddRange(FlattenViewStepDepartments(node.Children));
        }

        return result;
    }

    private async Task LoadIdentityUserLookupAsync()
    {
        var result = await WorkflowStepAssignmentsAppService.GetIdentityUserLookupAsync(new LookupRequestDto
        {
            MaxResultCount = 50
        });
        IdentityUsersCollection = result.Items;
    }

    private void InitializeViewStepScopeEditors()
    {
        ViewStepScopeEditors = new List<ViewStepScopeEditorState>();
        if (WorkflowSubmitInfo == null)
        {
            return;
        }

        foreach (var step in WorkflowSubmitInfo.Steps.Where(s => s.IsViewStep).OrderBy(s => s.Order))
        {
            var editor = new ViewStepScopeEditorState
            {
                StepId = step.StepId,
                StepName = step.Name
            };

            if (step.TemplateOrganizationUnitIds.Any())
            {
                editor.SelectedDepartments = AllViewStepDepartmentsFlat
                    .Where(d => step.TemplateOrganizationUnitIds.Contains(d.Id))
                    .ToList();
            }

            if (step.TemplateUserIds.Any())
            {
                editor.SelectedUsers = IdentityUsersCollection
                    .Where(u => step.TemplateUserIds.Contains(u.Id))
                    .ToList();
            }

            ViewStepScopeEditors.Add(editor);
        }
    }

    private bool ValidateViewScopeSelections()
    {
        foreach (var editor in ViewStepScopeEditors)
        {
            if (!editor.SelectedDepartments.Any() && !editor.SelectedUsers.Any())
            {
                return false;
            }
        }

        return true;
    }

    private List<WorkflowStepViewScopeSelectionDto> BuildViewStepScopeSelections()
    {
        return ViewStepScopeEditors
            .Select(editor => new WorkflowStepViewScopeSelectionDto
            {
                StepId = editor.StepId,
                OrganizationUnitIds = editor.SelectedDepartments.Select(d => d.Id).Distinct().ToList(),
                UserIds = editor.SelectedUsers.Select(u => u.Id).Distinct().ToList()
            })
            .ToList();
    }

    private Task OnViewStepDepartmentsChanged(ViewStepScopeEditorState editor, List<DepartmentTreeView> items)
    {
        editor.SelectedDepartments = items ?? new List<DepartmentTreeView>();
        return InvokeAsync(StateHasChanged);
    }

    private async Task<List<LookupDto<Guid>>> GetIdentityUserCollectionLookupAsync(
        IReadOnlyList<LookupDto<Guid>> items,
        string filter,
        CancellationToken token)
    {
        var result = await WorkflowStepAssignmentsAppService.GetIdentityUserLookupAsync(new LookupRequestDto
        {
            Filter = filter,
            MaxResultCount = 20
        });
        return result.Items.ToList();
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

    private bool HasPreSelectedDocument => PreSelectedDocument != null;
}
