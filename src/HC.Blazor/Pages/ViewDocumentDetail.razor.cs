using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Blazorise;
using HC.Documents;
using HC.DocumentFiles;
using HC.Shared;
using HC.MasterDatas;
using Volo.Abp.AspNetCore.Components.Web.Theming.PageToolbars;
using Volo.Abp.BlobStoring;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using HC.DocumentAssignments;
using Volo.Abp.AspNetCore.Components.Messages;
namespace HC.Blazor.Pages;

public partial class ViewDocumentDetail
{
    [Parameter] public Guid DocumentId { get; set; }

    [SupplyParameterFromQuery(Name = "id")]
    public Guid? DocumentIdQuery { get; set; }
    [SupplyParameterFromQuery(Name = "sourceType")]
    public int? SourceType { get; set; }

    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems { get; } = new();
    protected string PageTitle => L["ViewDocumentDetail"];
    protected DocumentWithNavigationPropertiesDto? CurrentDocument { get; set; }
    protected PageToolbar Toolbar { get; } = new PageToolbar();

    // Document data (read-only)
    private DocumentDto? DocumentData { get; set; }

    // MasterData collections for display
    private IReadOnlyList<LookupDto<Guid>> TypeMasterDataCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> UrgencyLevelMasterDataCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> SecrecyLevelMasterDataCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> FieldMasterDataCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> StatusMasterDataCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> UnitsCollection { get; set; } = new List<LookupDto<Guid>>();

    // Display values
    private string? TypeName { get; set; }
    private string? FieldName { get; set; }
    private string? UrgencyLevelName { get; set; }
    private string? SecrecyLevelName { get; set; }
    private string? StatusName { get; set; }
    private string? UnitName { get; set; }

    // Files
    private IReadOnlyList<DocumentFileWithNavigationPropertiesDto> DocumentFilesList { get; set; } = new List<DocumentFileWithNavigationPropertiesDto>();

    // PDF viewer
    private string? PdfFileUrl { get; set; } = "https://pdfobject.com/pdf/sample.pdf";
    private bool IsPdfAvailable { get; set; } = false;

    // Document revocation status
    private bool IsDocumentRevoked { get; set; } = false;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            if (DocumentId == Guid.Empty && DocumentIdQuery.HasValue)
            {
                DocumentId = DocumentIdQuery.Value;
            }

            if (DocumentId != Guid.Empty)
            {
                await LoadDocumentAsync();
            }

            await SetBreadcrumbItemsAsync();
            await SetToolbarItemsAsync();

            await InvokeAsync(StateHasChanged);
        }
    }

    protected virtual ValueTask SetBreadcrumbItemsAsync()
    {
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["DocumentAssignments"], "/manage-documents"));
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["ViewDocumentDetail"]));
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["Back"], () =>
        {
            // Use sourceType from query, or fallback to document's SourceType when available
            var effectiveSourceType = SourceType
                ?? (CurrentDocument != null ? (int?)CurrentDocument.Document.SourceType : null);
            var sourceTypeParam = effectiveSourceType.HasValue ? $"?sourceType={effectiveSourceType.Value}" : "";
            NavigationManager.NavigateTo("/manage-documents" + sourceTypeParam);
            return Task.CompletedTask;
        }, IconName.ArrowLeft);

        return ValueTask.CompletedTask;
    }

    private async Task LoadDocumentAsync()
    {
        try
        {
            CurrentDocument = await DocumentsAppService.GetWithNavigationPropertiesAsync(DocumentId);
            DocumentData = CurrentDocument.Document;

            // Load lookup data first
            await LoadLookupDataAsync();

            // Load display names
            await LoadDisplayNamesAsync();

            // Load document files
            await LoadDocumentFilesAsync();

            // Load PDF URL if file exists and is PDF
            await LoadPdfUrlAsync();

            // Check if document is revoked for current user
            await CheckDocumentRevocationAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    // Check if document assignment status is REVOKE for current user
    private async Task CheckDocumentRevocationAsync()
    {
        try
        {
            if (CurrentUser.Id == null)
            {
                return;
            }

            var assignments = await DocumentAssignmentsAppService.GetListAsync(new GetDocumentAssignmentsInput
            {
                DocumentId = DocumentId,
                ReceiverUserId = CurrentUser.Id,
                MaxResultCount = 1,
                SkipCount = 0
            });

            if (assignments != null && assignments.TotalCount > 0)
            {
                var assignment = assignments.Items.FirstOrDefault();
                if (assignment != null && assignment.DocumentAssignment.Status == DocumentAssignmentStatus.REVOKE.ToString())
                {
                    IsDocumentRevoked = true;
                }
            }
        }
        catch (Exception)
        {
            // Ignore error checking revocation status
            IsDocumentRevoked = false;
        }
    }

    private async Task LoadLookupDataAsync()
    {
        await Task.WhenAll(
            GetTypeMasterDataLookupAsync(TypeMasterDataCollection, "", CancellationToken.None),
            GetUrgencyLevelMasterDataLookupAsync(UrgencyLevelMasterDataCollection, "", CancellationToken.None),
            GetSecrecyLevelMasterDataLookupAsync(SecrecyLevelMasterDataCollection, "", CancellationToken.None),
            GetFieldMasterDataLookupAsync(FieldMasterDataCollection, "", CancellationToken.None),
            GetStatusMasterDataLookupAsync(StatusMasterDataCollection, "", CancellationToken.None),
            GetUnitLookupAsync(UnitsCollection, "", CancellationToken.None)
        );
    }

    private async Task<List<LookupDto<Guid>>> GetTypeMasterDataLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var result = await MasterDatasAppService.GetListAsync(new GetMasterDatasInput
        {
            Type = MasterDataType.DocumentType.GetTypeValue(),
            FilterText = filter,
            MaxResultCount = 1000,
            SkipCount = 0
        });
        TypeMasterDataCollection = result.Items.Select(x => new LookupDto<Guid> { Id = x.Id, DisplayName = x.Name }).ToList();
        return TypeMasterDataCollection.ToList();
    }

    private async Task<List<LookupDto<Guid>>> GetUrgencyLevelMasterDataLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var result = await MasterDatasAppService.GetListAsync(new GetMasterDatasInput
        {
            Type = MasterDataType.UrgencyLevel.GetTypeValue(),
            FilterText = filter,
            MaxResultCount = 1000,
            SkipCount = 0
        });
        UrgencyLevelMasterDataCollection = result.Items.Select(x => new LookupDto<Guid> { Id = x.Id, DisplayName = x.Name }).ToList();
        return UrgencyLevelMasterDataCollection.ToList();
    }

    private async Task<List<LookupDto<Guid>>> GetSecrecyLevelMasterDataLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var result = await MasterDatasAppService.GetListAsync(new GetMasterDatasInput
        {
            Type = MasterDataType.SecrecyLevel.GetTypeValue(),
            FilterText = filter,
            MaxResultCount = 1000,
            SkipCount = 0
        });
        SecrecyLevelMasterDataCollection = result.Items.Select(x => new LookupDto<Guid> { Id = x.Id, DisplayName = x.Name }).ToList();
        return SecrecyLevelMasterDataCollection.ToList();
    }

    private async Task<List<LookupDto<Guid>>> GetFieldMasterDataLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var result = await MasterDatasAppService.GetListAsync(new GetMasterDatasInput
        {
            Type = MasterDataType.Field.GetTypeValue(),
            FilterText = filter,
            MaxResultCount = 1000,
            SkipCount = 0
        });
        FieldMasterDataCollection = result.Items.Select(x => new LookupDto<Guid> { Id = x.Id, DisplayName = x.Name }).ToList();
        return FieldMasterDataCollection.ToList();
    }

    private async Task<List<LookupDto<Guid>>> GetStatusMasterDataLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var result = await MasterDatasAppService.GetListAsync(new GetMasterDatasInput
        {
            Type = MasterDataType.Status.GetTypeValue(),
            FilterText = filter,
            MaxResultCount = 1000,
            SkipCount = 0
        });
        StatusMasterDataCollection = result.Items.Select(x => new LookupDto<Guid> { Id = x.Id, DisplayName = x.Name }).ToList();
        return StatusMasterDataCollection.ToList();
    }

    private async Task<List<LookupDto<Guid>>> GetUnitLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var result = await DocumentsAppService.GetUnitLookupAsync(new LookupRequestDto { Filter = filter });
        UnitsCollection = result.Items;
        return UnitsCollection.ToList();
    }

    private async Task<LookupDto<Guid>?> GetMasterDataByIdAsync(Guid id, MasterDataType type)
    {
        var result = await MasterDatasAppService.GetListAsync(new GetMasterDatasInput
        {
            Type = type.GetTypeValue(),
            MaxResultCount = 1000,
            SkipCount = 0
        });
        var masterData = result.Items.FirstOrDefault(x => x.Id == id);
        if (masterData != null)
        {
            return new LookupDto<Guid> { Id = masterData.Id, DisplayName = masterData.Name };
        }
        return null;
    }

    private async Task<LookupDto<Guid>?> GetUnitByIdAsync(Guid? id)
    {
        if (!id.HasValue) return null;
        var result = await DocumentsAppService.GetUnitLookupAsync(new LookupRequestDto { Filter = "" });
        return result.Items.FirstOrDefault(x => x.Id == id.Value);
    }

    private async Task LoadDisplayNamesAsync()
    {
        if (DocumentData == null) return;

        if (DocumentData.TypeId != default)
        {
            var typeData = await GetMasterDataByIdAsync(DocumentData.TypeId, MasterDataType.DocumentType);
            TypeName = typeData?.DisplayName;
        }

        if (DocumentData.FieldId.HasValue)
        {
            var fieldData = await GetMasterDataByIdAsync(DocumentData.FieldId.Value, MasterDataType.Field);
            FieldName = fieldData?.DisplayName;
        }

        if (DocumentData.UrgencyLevelId != default)
        {
            var urgencyData = await GetMasterDataByIdAsync(DocumentData.UrgencyLevelId, MasterDataType.UrgencyLevel);
            UrgencyLevelName = urgencyData?.DisplayName;
        }

        if (DocumentData.SecrecyLevelId != default)
        {
            var secrecyData = await GetMasterDataByIdAsync(DocumentData.SecrecyLevelId, MasterDataType.SecrecyLevel);
            SecrecyLevelName = secrecyData?.DisplayName;
        }

        if (DocumentData.StatusId.HasValue)
        {
            var statusData = await GetMasterDataByIdAsync(DocumentData.StatusId.Value, MasterDataType.Status);
            StatusName = statusData?.DisplayName;
        }

        if (DocumentData.UnitId.HasValue)
        {
            var unitData = await GetUnitByIdAsync(DocumentData.UnitId);
            UnitName = unitData?.DisplayName;
        }
    }

    private async Task LoadDocumentFilesAsync()
    {
        try
        {
            var result = await DocumentFilesAppService.GetListAsync(new GetDocumentFilesInput
            {
                DocumentId = DocumentId,
                MaxResultCount = 1000,
                SkipCount = 0
            });
            DocumentFilesList = result.Items;
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    // Load PDF URL for viewer
    private async Task LoadPdfUrlAsync()
    {
        IsPdfAvailable = false;
        PdfFileUrl = null;

        // Check if there's a file in DocumentFilesList
        var firstFile = DocumentFilesList.FirstOrDefault();
        if (firstFile == null || string.IsNullOrEmpty(firstFile.DocumentFile.Path))
        {
            return;
        }

        // Check if file is PDF
        if (!IsPdfFileExtension(firstFile.DocumentFile.Name))
        {
            return;
        }

        try
        {
            // Get watermarked PDF from API (user + timestamp stamped)
            var fileBytes = await DocumentPdfViewerAppService.GetWatermarkedPdfAsync(new HC.DocumentPdfViewer.GetWatermarkedPdfInput
            {
                BlobPath = firstFile.DocumentFile.Path,
                Action = "view"
            });
            
            // Create data URL for PDF
            var base64 = Convert.ToBase64String(fileBytes);
            PdfFileUrl = $"data:application/pdf;base64,{base64}";
            IsPdfAvailable = true;
        }
        catch
        {
            // File not found or other error - hide PDF viewer
            IsPdfAvailable = false;
            PdfFileUrl = null;
        }
    }

    private bool IsPdfFileExtension(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;
        
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension == ".pdf";
    }

    private async Task ViewFileAsync(DocumentFileDto file)
    {
        // Load the selected file into PDF viewer
        if (string.IsNullOrEmpty(file.Path))
        {
            await UiMessageService.Warn(L["FileNotFound"],
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            IsPdfAvailable = false;
            PdfFileUrl = null;
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (!IsPdfFileExtension(file.Name))
        {
            await UiMessageService.Warn(L["NotAPdfFile"],
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            IsPdfAvailable = false;
            PdfFileUrl = null;
            await InvokeAsync(StateHasChanged);
            return;
        }

        try
        {
            var fileBytes = await DocumentPdfViewerAppService.GetWatermarkedPdfAsync(new HC.DocumentPdfViewer.GetWatermarkedPdfInput
            {
                BlobPath = file.Path,
                Action = "view"
            });
            if (fileBytes != null && fileBytes.Length > 0)
            {
                var base64 = Convert.ToBase64String(fileBytes);
                PdfFileUrl = $"data:application/pdf;base64,{base64}";
                IsPdfAvailable = true;
                await InvokeAsync(StateHasChanged);
            }
            else
            {
                await UiMessageService.Warn(L["FileNotFound"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                IsPdfAvailable = false;
                PdfFileUrl = null;
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
            IsPdfAvailable = false;
            PdfFileUrl = null;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task DownloadFileAsync(DocumentFileDto file)
    {
        try
        {
            if (string.IsNullOrEmpty(file.Path))
            {
                await UiMessageService.Warn(L["FileNotFound"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }
            var fileBytes = await DocumentPdfViewerAppService.GetWatermarkedPdfAsync(new HC.DocumentPdfViewer.GetWatermarkedPdfInput
            {
                BlobPath = file.Path,
                Action = "download"
            });
            if (fileBytes != null && fileBytes.Length > 0)
            {
                var base64 = Convert.ToBase64String(fileBytes);
                var fileName = file.Name ?? "document.pdf";
                await JSRuntime.InvokeVoidAsync("downloadFile", fileName, "application/pdf", base64);
            }
            else
            {
                await UiMessageService.Warn(L["FileNotFound"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }
}
