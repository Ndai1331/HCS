using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Blazorise;
using Blazorise.RichTextEdit;
using HC.Documents;
using HC.DocumentFiles;
using HC.DocumentWorkflowInstances;
using HC.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Components.BlockUi;
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
    [Inject] private IDocumentsAppService DocumentsAppService { get; set; } = default!;
    [Inject] private IDocumentFilesAppService DocumentFilesAppService { get; set; } = default!;
    [Inject] private IBlockUiService BlockUiService { get; set; } = default!;
    [Inject] private IBlobContainer BlobContainer { get; set; } = default!;
    [Inject] private IClock Clock { get; set; } = default!;

    private Modal ModalRef { get; set; } = new();
    private Guid? SelectedWorkflowId { get; set; }
    private IReadOnlyList<LookupDto<Guid>> AvailableWorkflows { get; set; } = new List<LookupDto<Guid>>();
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
        PreSelectedDocument = preSelectedDocument;
        SelectedDocumentId = preSelectedDocument?.Document?.Id;
        SelectedDocumentDto = preSelectedDocument;
        SelectedWorkflowId = null;
        WorkflowSubmitInfo = null;
        UseTemplateFile = true;
        UseWorkflowTemplateFile = preSelectedDocument != null ? false : UseWorkflowTemplateFile;
        SigningContent = null;
        UploadedFiles.Clear();
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

        await LoadWorkflowLookupAsync();
        await InvokeAsync(ModalRef.Show);
        await InvokeAsync(StateHasChanged);
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
                await BlockUiService.UnBlock();
                return;
            }

            var input = new SubmitToWorkflowInput
            {
                DocumentId = UseWorkflowTemplateFile ? null : SelectedDocumentId,
                WorkflowId = SelectedWorkflowId.Value,
                UseWorkflowTemplateFile = UseWorkflowTemplateFile,
                UseTemplateFile = UseTemplateFile,
                SigningContent = signingContent,
                AttachedFileIds = UploadedFiles.Any()
                    ? UploadedFiles.Select(f => f.DocumentFileId).ToList()
                    : null
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
            await BlockUiService.UnBlock();
        }
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
