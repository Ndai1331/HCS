using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Blazorise;
using HC.Documents;
using HC.DocumentFiles;
using HC.MasterDatas;
using HC.Permissions;
using HC.Shared;
using HC.Blazor.Shared;
using HC.DocumentHistories;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Volo.Abp.BlobStoring;
using Volo.Abp.Application.Dtos;
using Blazorise.PdfViewer;
using Volo.Abp.AspNetCore.Components.Web.Theming.PageToolbars;
using System.Text.Json;
using Volo.Abp.AspNetCore.Components.Messages;
using Blazorise.DataGrid;
namespace HC.Blazor.Pages.Documents;
public partial class DocumentDetail : HCComponentBase
{
    [Parameter] public Guid DocumentId { get; set; }

    [SupplyParameterFromQuery(Name = "id")]
    public Guid? DocumentIdQuery { get; set; }

    [SupplyParameterFromQuery(Name = "sourceType")]
    public int? SourceType { get; set; }

    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems { get; } = new();

    protected string PageTitle => DocumentId == Guid.Empty ? L["NewDocument"] : L["EditDocument"];
    protected DocumentWithNavigationPropertiesDto? CurrentDocument { get; set; }
    protected PageToolbar Toolbar { get; } = new PageToolbar();
    private bool CanEditDocument { get; set; }
    private bool CanCreateDocument { get; set; }
    private bool CanDeleteDocumentFile { get; set; }


    // Document data
    private DocumentCreateDto? DocumentCreateData { get; set; } = new DocumentCreateDto();
    private DocumentUpdateDto? DocumentUpdateData { get; set; } = new DocumentUpdateDto();

    // PDF viewer refs
    private PdfViewer? EditPdfViewerRef { get; set; }
    private PdfViewer? CreatePdfViewerRef { get; set; }

    // Validation helpers using shared ValidationHelper
    private ValidationHelper CreateValidation { get; } = new();
    private ValidationHelper EditValidation { get; } = new();
    
    // Helper methods to get field errors (for backward compatibility with Razor markup)
    private string? GetCreateFieldError(string fieldName) => CreateValidation.GetFieldError(fieldName);
    private string? GetEditFieldError(string fieldName) => EditValidation.GetFieldError(fieldName);
    private bool HasCreateFieldError(string fieldName) => CreateValidation.HasFieldError(fieldName);
    private bool HasEditFieldError(string fieldName) => EditValidation.HasFieldError(fieldName);
    
    // Validation error keys (for backward compatibility)
    private string? CreateDocumentValidationErrorKey => CreateValidation.FirstValidationErrorKey;
    private string? EditDocumentValidationErrorKey => EditValidation.FirstValidationErrorKey;

    // MasterData collections
    private IReadOnlyList<LookupDto<Guid>> TypeMasterDataCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> UrgencyLevelMasterDataCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> SecrecyLevelMasterDataCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> FieldMasterDataCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> StatusMasterDataCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> UnitsCollection { get; set; } = new List<LookupDto<Guid>>();

    // Selected values for Select2
    private List<LookupDto<Guid>> SelectedTypeMasterData { get; set; } = new();
    private List<LookupDto<Guid>> SelectedUrgencyLevelMasterData { get; set; } = new();
    private List<LookupDto<Guid>> SelectedSecrecyLevelMasterData { get; set; } = new();
    private List<LookupDto<Guid>> SelectedFieldMasterData { get; set; } = new();
    private List<LookupDto<Guid>> SelectedStatusMasterData { get; set; } = new();
    private List<LookupDto<Guid>> SelectedUnit { get; set; } = new();

    // Document Source Type (Archive/Personal)
    private DocumentSourceType SelectedSourceType { get; set; } = DocumentSourceType.Archive;

    // File upload
    private IFileEntry? SelectedFile { get; set; }
    private string UploadedFilePath { get; set; } = string.Empty;
    private string UploadedFileHash { get; set; } = string.Empty;
    private bool IsUploading { get; set; }
    private int FilePickerProgress { get; set; }
    private FilePicker? DocumentFilePicker { get; set; }

    private IReadOnlyList<DocumentFileWithNavigationPropertiesDto> DocumentFilesList { get; set; } = new List<DocumentFileWithNavigationPropertiesDto>();

    // Document Histories with pagination
    private IReadOnlyList<DocumentHistoryWithNavigationPropertiesDto> DocumentHistoriesList { get; set; } = new List<DocumentHistoryWithNavigationPropertiesDto>();
    private int DocumentHistoriesTotalCount { get; set; }
    private int DocumentHistoriesPageSize { get; } = 10;
    private int DocumentHistoriesCurrentPage { get; set; } = 1;

    // PDF viewer
    private string? PdfFileUrl { get; set; } = "https://pdfobject.com/pdf/sample.pdf";
    private bool IsPdfFile { get; set; }

    // PDF Viewer Modal
    private Modal? PdfViewerModal { get; set; }

    // DatePicker refs
    private DatePicker<DateTime>? EditIncommingDateDatePicker { get; set; }
    private DatePicker<DateTime>? CreateIncommingDateDatePicker { get; set; }

  
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            await SetPermissionsAsync();
            await LoadLookupDataAsync();
            BreadcrumbItems.Clear();
            BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["Documents"], "/manage-documents"));
            await SetToolbarItemsAsync();

            if (DocumentId == Guid.Empty && DocumentIdQuery.HasValue)
            {
                DocumentId = DocumentIdQuery.Value;
            }

            if (DocumentId == Guid.Empty)
            {
                BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["NewDocument"]));

                // Get sourceType from query parameter
                DocumentSourceType defaultSourceType = DocumentSourceType.Archive;
                var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
                var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
                if (query.TryGetValue("sourceType", out var sourceTypeValue) && int.TryParse(sourceTypeValue, out var sourceTypeInt))
                {
                    defaultSourceType = (DocumentSourceType)sourceTypeInt;
                }

                DocumentCreateData = new DocumentCreateDto
                {
                    CompletedTime = DateTime.Now,
                    IncommingDate = DateTime.Now,
                    StorageNumber = GenerateStorageNumber(),
                    UrgencyLevelId = UrgencyLevelMasterDataCollection.FirstOrDefault()?.Id ?? Guid.Empty,
                    SecrecyLevelId = SecrecyLevelMasterDataCollection.FirstOrDefault()?.Id ?? Guid.Empty,
                    StatusId = StatusMasterDataCollection.FirstOrDefault()?.Id ?? Guid.Empty,
                    SourceType = defaultSourceType,
                };
                
                SelectedSourceType = defaultSourceType;

                if (DocumentCreateData.UrgencyLevelId != default)
                {
                    var urgencyData = await GetMasterDataByIdAsync(DocumentCreateData.UrgencyLevelId, MasterDataType.UrgencyLevel);
                    Logger.LogInformation($"UrgencyData: {JsonSerializer.Serialize(urgencyData)}");
                    if (urgencyData != null)
                        SelectedUrgencyLevelMasterData = new List<LookupDto<Guid>> { urgencyData };
                }
                if (DocumentCreateData.SecrecyLevelId != default)
                {
                    var secrecyData = await GetMasterDataByIdAsync(DocumentCreateData.SecrecyLevelId, MasterDataType.SecrecyLevel);
                    Logger.LogInformation($"SecrecyData: {JsonSerializer.Serialize(secrecyData)}");
                    if (secrecyData != null)
                        SelectedSecrecyLevelMasterData = new List<LookupDto<Guid>> { secrecyData };
                }

                if (DocumentCreateData.StatusId.HasValue && DocumentCreateData.StatusId.Value != default)
                {
                    var statusData = await GetMasterDataByIdAsync(DocumentCreateData.StatusId.Value, MasterDataType.Status);
                    Logger.LogInformation($"StatusData: {JsonSerializer.Serialize(statusData)}");
                    if (statusData != null)
                        SelectedStatusMasterData = new List<LookupDto<Guid>> { statusData };
                }

                DocumentUpdateData = null;
            }
            else
            {
                BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["Details"]));
                await LoadDocumentAsync();
            }
            await BlockUiService.UnBlock();
            await InvokeAsync(StateHasChanged);
        }
    }
    private async Task SetPermissionsAsync()
    {
        CanCreateDocument = await HasRoleHelper.HasRoleAsync(AuthorizationService, HCPermissions.Documents.Create);
        CanEditDocument = await HasRoleHelper.HasRoleAsync(AuthorizationService, HCPermissions.Documents.Edit);
        CanDeleteDocumentFile = await HasRoleHelper.HasRoleAsync(AuthorizationService, HCPermissions.DocumentFiles.Delete);
    }
    protected virtual ValueTask SetToolbarItemsAsync()
    {   
        Toolbar.AddButton(L["Back"], () =>
        {
            var sourceTypeParam = SourceType.HasValue ? $"?sourceType={SourceType.Value}" : "";
            NavigationManager.NavigateTo("/manage-documents" + sourceTypeParam);
            return Task.CompletedTask;
        }, IconName.ArrowLeft);

        if (DocumentId == Guid.Empty && CanCreateDocument) 
        {
            Toolbar.AddButton(L["Save"], OnSave, IconName.Save, Color.Primary);
        }else if (DocumentId != Guid.Empty && CanEditDocument)
        {
            Toolbar.AddButton(L["Edit"], OnSave, IconName.Edit, Color.Primary);
        }
        if (CurrentDocument != null && CanDeleteDocumentFile)
        {
            Toolbar.AddButton(L["Delete"],DeleteDocumentAsync, IconName.Delete, Color.Danger);
        }
        return ValueTask.CompletedTask;
    }

    private async Task LoadDocumentAsync()
    {
        try
        {
            CurrentDocument = await DocumentsAppService.GetWithNavigationPropertiesAsync(DocumentId);
            if (CurrentDocument != null)
            {
                DocumentUpdateData = ObjectMapper.Map<DocumentDto, DocumentUpdateDto>(CurrentDocument.Document);
                DocumentCreateData = null;

                if (CurrentDocument.Document.TypeId != default)
                {
                    var typeData = await GetMasterDataByIdAsync(CurrentDocument.Document.TypeId, MasterDataType.DocumentType);
                    if (typeData != null)
                        SelectedTypeMasterData = new List<LookupDto<Guid>> { typeData };
                }
                if (CurrentDocument.Document.UrgencyLevelId != default)
                {
                    var urgencyData = await GetMasterDataByIdAsync(CurrentDocument.Document.UrgencyLevelId, MasterDataType.UrgencyLevel);
                    if (urgencyData != null)
                        SelectedUrgencyLevelMasterData = new List<LookupDto<Guid>> { urgencyData };
                }
                if (CurrentDocument.Document.SecrecyLevelId != default)
                {
                    var secrecyData = await GetMasterDataByIdAsync(CurrentDocument.Document.SecrecyLevelId, MasterDataType.SecrecyLevel);
                    if (secrecyData != null)
                        SelectedSecrecyLevelMasterData = new List<LookupDto<Guid>> { secrecyData };
                }
                if (CurrentDocument.Document.FieldId.HasValue)
                {
                    var fieldData = await GetMasterDataByIdAsync(CurrentDocument.Document.FieldId.Value, MasterDataType.Field);
                    if (fieldData != null)
                        SelectedFieldMasterData = new List<LookupDto<Guid>> { fieldData };
                }
                if (CurrentDocument.Document.StatusId.HasValue)
                {
                    var statusData = await GetMasterDataByIdAsync(CurrentDocument.Document.StatusId.Value, MasterDataType.Status);
                    if (statusData != null)
                        SelectedStatusMasterData = new List<LookupDto<Guid>> { statusData };
                }
                if (CurrentDocument.Document.UnitId.HasValue)
                {
                    var unitData = await GetUnitByIdAsync(CurrentDocument.Document.UnitId.Value);
                    if (unitData != null)
                        SelectedUnit = new List<LookupDto<Guid>> { unitData };
                }

                // Load SourceType
                SelectedSourceType = CurrentDocument.Document.SourceType;

                // Load document files
                Logger.LogInformation($"LoadDocumentAsync: Calling LoadDocumentFilesAsync");
                await LoadDocumentFilesAsync();

                // Load document histories
                Logger.LogInformation($"LoadDocumentAsync: Calling LoadDocumentHistoriesAsync");
                await LoadDocumentHistoriesAsync();
                
                // Load PDF URL if file exists and is PDF
                await LoadPdfUrlAsync();
            }
            else
            {
                Logger.LogWarning($"LoadDocumentAsync: Document not found. DocumentId: {DocumentId}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Error loading document. DocumentId: {DocumentId}");
            throw;
        }
    }

    private async Task LoadDocumentFilesAsync()
    {
        try
        {
            Logger.LogInformation($"LoadDocumentFilesAsync called. DocumentId: {DocumentId}");
            
            if (DocumentId == Guid.Empty)
            {
                Logger.LogWarning("LoadDocumentFilesAsync: DocumentId is Empty");
                return;
            }

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
            Logger.LogError(ex, $"Error loading document files for DocumentId: {DocumentId}");
            throw;
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
            SkipCount = 0,
            Sorting = "SortOrder asc"
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
            SkipCount = 0,
            Sorting = "SortOrder asc"
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
            SkipCount = 0,
            Sorting = "SortOrder asc"
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
            SkipCount = 0,
            Sorting = "SortOrder asc"
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
            SkipCount = 0,
            Sorting = "SortOrder asc"
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
            SkipCount = 0,
            Sorting = "SortOrder asc"
        });
        var masterData = result.Items.FirstOrDefault(x => x.Id == id);
        if (masterData != null)
        {
            return new LookupDto<Guid> { Id = masterData.Id, DisplayName = masterData.Name };
        }
        return null;
    }

    private async Task<LookupDto<Guid>?> GetUnitByIdAsync(Guid id)
    {
        var result = await DocumentsAppService.GetUnitLookupAsync(new LookupRequestDto { Filter = "" });
        return result.Items.FirstOrDefault(x => x.Id == id);
    }

    // Select2 change handlers
    private void OnTypeIdChanged()
    {
        if (SelectedTypeMasterData?.Any() == true)
        {
            if (DocumentUpdateData != null)
            {
                DocumentUpdateData.TypeId = SelectedTypeMasterData[0].Id;
                EditValidation.RemoveFieldError("Type");
            }
            else if (DocumentCreateData != null)
            {
                DocumentCreateData.TypeId = SelectedTypeMasterData[0].Id;
                CreateValidation.RemoveFieldError("Type");
            }
        }
        InvokeAsync(StateHasChanged);
    }

    private void OnUrgencyLevelIdChanged()
    {
        if (SelectedUrgencyLevelMasterData?.Any() == true)
        {
            if ( DocumentUpdateData != null)
            {
                DocumentUpdateData.UrgencyLevelId = SelectedUrgencyLevelMasterData[0].Id;
                EditValidation.RemoveFieldError("UrgencyLevel");
            }
            else if (DocumentCreateData != null)
            {
                DocumentCreateData.UrgencyLevelId = SelectedUrgencyLevelMasterData[0].Id;
                CreateValidation.RemoveFieldError("UrgencyLevel");
            }
        }
        InvokeAsync(StateHasChanged);
    }

    private void OnSecrecyLevelIdChanged()
    {
        if (SelectedSecrecyLevelMasterData?.Any() == true)
        {
            if (DocumentUpdateData != null)
            {
                DocumentUpdateData.SecrecyLevelId = SelectedSecrecyLevelMasterData[0].Id;
                EditValidation.RemoveFieldError("SecrecyLevel");
            }
            else if (DocumentCreateData != null)
            {
                DocumentCreateData.SecrecyLevelId = SelectedSecrecyLevelMasterData[0].Id;
                CreateValidation.RemoveFieldError("SecrecyLevel");
            }
        }
        InvokeAsync(StateHasChanged);
    }

    private void OnFieldIdChanged()
    {
        if (SelectedFieldMasterData?.Any() == true)
        {
            if (DocumentUpdateData != null)
            {
                DocumentUpdateData.FieldId = SelectedFieldMasterData[0].Id;
                EditValidation.RemoveFieldError("Field");
            }
            else if (DocumentCreateData != null)
            {
                DocumentCreateData.FieldId = SelectedFieldMasterData[0].Id;
                CreateValidation.RemoveFieldError("Field");
            }
        }
        else
        {
            if (DocumentUpdateData != null)
            {
                DocumentUpdateData.FieldId = null;
                EditValidation.RemoveFieldError("Field");
            }
            else if (DocumentCreateData != null)
            {
                DocumentCreateData.FieldId = null;
                CreateValidation.RemoveFieldError("Field");
            }
        }
        InvokeAsync(StateHasChanged);
    }

    private void OnStatusIdChanged()
    {
        if (SelectedStatusMasterData?.Any() == true)
        {
            if (DocumentUpdateData != null)
            {
                DocumentUpdateData.StatusId = SelectedStatusMasterData[0].Id;
                EditValidation.RemoveFieldError("Status");
            }
            else if (DocumentCreateData != null)
            {
                DocumentCreateData.StatusId = SelectedStatusMasterData[0].Id;
                CreateValidation.RemoveFieldError("Status");
            }
        }
        else
        {
            if ( DocumentUpdateData != null)
            {
                DocumentUpdateData.StatusId = null;
                EditValidation.RemoveFieldError("Status");
            }
            else if (DocumentCreateData != null)
            {
                DocumentCreateData.StatusId = null;
                CreateValidation.RemoveFieldError("Status");
            }
        }
        InvokeAsync(StateHasChanged);
    }

    private void OnUnitIdChanged()
    {
        if (SelectedUnit?.Any() == true)
        {
            if (DocumentUpdateData != null)
            {
                DocumentUpdateData.UnitId = SelectedUnit[0].Id;
                EditValidation.RemoveFieldError("Unit");
            }
            else if (DocumentCreateData != null)
            {
                DocumentCreateData.UnitId = SelectedUnit[0].Id;
                CreateValidation.RemoveFieldError("Unit");
            }
        }
        else
        {
            if (DocumentUpdateData != null)
            {
                DocumentUpdateData.UnitId = null;
                EditValidation.RemoveFieldError("Unit");
            }
            else if (DocumentCreateData != null)
            {
                DocumentCreateData.UnitId = null;
                CreateValidation.RemoveFieldError("Unit");
            }
        }
        InvokeAsync(StateHasChanged);
    }

    private void OnSourceTypeChanged()
    {
        if (DocumentUpdateData != null)
        {
            DocumentUpdateData.SourceType = SelectedSourceType;
        }
        else if (DocumentCreateData != null)
        {
            DocumentCreateData.SourceType = SelectedSourceType;
        }
        InvokeAsync(StateHasChanged);
    }

    private async Task OnFileChanged(FileChangedEventArgs e)
    {
        if (e.Files != null && e.Files.Any())
        {
            var file = e.Files.First();
            await UploadFileAsync(file);
        }

       
    }

    private async Task UploadFileAsync(IFileEntry file)
    {
        try
        {
            IsUploading = true;
            SelectedFile = file;
            FilePickerProgress = 0;

            using var memoryStream = new MemoryStream();

            // Open the file stream and store it in a variable to avoid premature disposal
            await using var fileStream = file.OpenReadStream(long.MaxValue);

            // Copy file data to memory stream
            await fileStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            var fileBytes = memoryStream.ToArray();

            // Calculate file hash
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(fileBytes);
            UploadedFileHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

            // Generate unique file name
            var fileName = $"{Guid.NewGuid()}_{file.Name}";
            var filePath = $"documents/{fileName}";

            // Upload to MinIO
            await BlobContainer.SaveAsync(filePath, fileBytes);

            UploadedFilePath = filePath;
            FilePickerProgress = 100;

            // Check if uploaded file is PDF and create URL
            if (IsPdfFileExtension(file.Name))
            {
                IsPdfFile = true;
                var base64 = Convert.ToBase64String(fileBytes);
                PdfFileUrl = $"data:application/pdf;base64,{base64}";
            }
            else
            {
                IsPdfFile = false;
                PdfFileUrl = "https://pdfobject.com/pdf/sample.pdf";
            }
            await UiMessageService.Success(L["FileUploadedSuccessfully"],
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));

        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
            UploadedFilePath = string.Empty;
            FilePickerProgress = 0;
            PdfFileUrl = "https://pdfobject.com/pdf/sample.pdf";
            IsPdfFile = false;
        }
        finally
        {
            IsUploading = false;
            if (DocumentFilePicker != null)
            {
                await DocumentFilePicker.Clear();
            }
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task OnSave()
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            
            if (DocumentUpdateData != null)
            {
                if (!ValidateEditDocument())
                {
                    await UiMessageService.Warn(L[EditDocumentValidationErrorKey ?? "ValidationError"],
                    options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                    await InvokeAsync(StateHasChanged);
                    return;
                }
            }
            else if (DocumentCreateData != null)
            {
                if (!ValidateCreateDocument())
                {
                    await UiMessageService.Warn(L[CreateDocumentValidationErrorKey ?? "ValidationError"],
                    options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                    await InvokeAsync(StateHasChanged);
                    return;
                }
            }
            else
            {
                // Should not happen, but handle gracefully
                await UiMessageService.Warn(L["PleaseFillRequiredFields"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            // Validate required file is uploaded for create mode
            if (DocumentId == Guid.Empty && string.IsNullOrEmpty(UploadedFilePath))
            {
                await UiMessageService.Warn(L["PleaseUploadFileFirst"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            DocumentDto savedDocument;

            if (DocumentId != Guid.Empty && DocumentUpdateData != null)
            {
                savedDocument = await DocumentsAppService.UpdateAsync(DocumentId, DocumentUpdateData);

                // Save new file on edit when user uploaded a replacement
                if (!string.IsNullOrEmpty(UploadedFilePath) && SelectedFile != null)
                {
                    await DocumentFilesAppService.CreateAsync(new DocumentFileCreateDto
                    {
                        DocumentId = savedDocument.Id,
                        Name = SelectedFile.Name,
                        Path = UploadedFilePath,
                        Hash = UploadedFileHash,
                        IsSigned = false,
                        UploadedAt = DateTime.Now
                    });
                }
            }
            else if (DocumentCreateData != null)
            {
                savedDocument = await DocumentsAppService.CreateAsync(DocumentCreateData);

                // Save file to DocumentFiles table
                if (!string.IsNullOrEmpty(UploadedFilePath) && SelectedFile != null)
                {
                    await DocumentFilesAppService.CreateAsync(new DocumentFileCreateDto
                    {
                        DocumentId = savedDocument.Id,
                        Name = SelectedFile.Name,
                        Path = UploadedFilePath,
                        Hash = UploadedFileHash,
                        IsSigned = false,
                        UploadedAt = DateTime.Now
                    });
                }
            }
            else
            {
                return;
            }

            await UiMessageService.Success(L["SuccessfullySaved"],
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            var sourceTypeParam = SourceType.HasValue ? $"?sourceType={SourceType.Value}" : "";
            NavigationManager.NavigateTo($"/document-detail/{savedDocument.Id}{sourceTypeParam}", true);
        }
        catch (Exception ex)
        {
            await UiMessageService.Error(ex.Message,
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
        }
        finally
        {
            await BlockUiService.UnBlock();
            await InvokeAsync(StateHasChanged);
        }
    }
    private async Task ClosePdfViewerModalAsync()
    {
        if (PdfViewerModal != null)
        {
            await PdfViewerModal.Hide();
        }
    }

    private bool ValidateCreateDocument()
    {
        CreateValidation.Reset();

        // Required: StorageNumber
        CreateValidation.ValidateRequiredString("StorageNumber", DocumentCreateData?.StorageNumber, "StorageNumberRequired", () => L["StorageNumberRequired"]);

        // Required: Title
        CreateValidation.ValidateRequiredString("Title", DocumentCreateData?.Title, "TitleRequired", () => L["TitleRequired"]);

        // Required: Type
        CreateValidation.ValidateRequiredCollection("Type", SelectedTypeMasterData, "TypeRequired", () => L["TypeRequired"]);

        // Required: UrgencyLevel
        CreateValidation.ValidateRequiredCollection("UrgencyLevel", SelectedUrgencyLevelMasterData, "UrgencyLevelRequired", () => L["UrgencyLevelRequired"]);

        // Required: SecrecyLevel
        CreateValidation.ValidateRequiredCollection("SecrecyLevel", SelectedSecrecyLevelMasterData, "SecrecyLevelRequired", () => L["SecrecyLevelRequired"]);

        // Required: DocumentNumber (No)
        CreateValidation.ValidateRequiredString("DocumentNumber", DocumentCreateData?.No, "DocumentNumberRequired", () => L["DocumentNumberRequired"]);

        // Required: Field
        CreateValidation.ValidateRequiredCollection("Field", SelectedFieldMasterData, "FieldRequired", () => L["FieldRequired"]);

        // Required: Unit (only when sourceType is not Personal)
        if (SelectedSourceType != DocumentSourceType.Personal)
        {
            CreateValidation.ValidateRequiredCollection("Unit", SelectedUnit, "UnitRequired", () => L["UnitRequired"]);
        }

        // Required: Status
        CreateValidation.ValidateRequiredCollection("Status", SelectedStatusMasterData, "StatusRequired", () => L["StatusRequired"]);

        return CreateValidation.IsValid;
    }

    private bool ValidateEditDocument()
    {
        EditValidation.Reset();

        // Required: StorageNumber
        EditValidation.ValidateRequiredString("StorageNumber", DocumentUpdateData?.StorageNumber, "StorageNumberRequired", () => L["StorageNumberRequired"]);

        // Required: Title
        EditValidation.ValidateRequiredString("Title", DocumentUpdateData?.Title, "TitleRequired", () => L["TitleRequired"]);

        // Required: Type
        EditValidation.ValidateRequiredCollection("Type", SelectedTypeMasterData, "TypeRequired", () => L["TypeRequired"]);

        // Required: UrgencyLevel
        EditValidation.ValidateRequiredCollection("UrgencyLevel", SelectedUrgencyLevelMasterData, "UrgencyLevelRequired", () => L["UrgencyLevelRequired"]);

        // Required: SecrecyLevel
        EditValidation.ValidateRequiredCollection("SecrecyLevel", SelectedSecrecyLevelMasterData, "SecrecyLevelRequired", () => L["SecrecyLevelRequired"]);

        // Required: DocumentNumber (No)
        EditValidation.ValidateRequiredString("DocumentNumber", DocumentUpdateData?.No, "DocumentNumberRequired", () => L["DocumentNumberRequired"]);

        // Required: Field
        EditValidation.ValidateRequiredCollection("Field", SelectedFieldMasterData, "FieldRequired", () => L["FieldRequired"]);

        // Required: Unit (only when sourceType is not Personal)
        if (SelectedSourceType != DocumentSourceType.Personal)
        {
            EditValidation.ValidateRequiredCollection("Unit", SelectedUnit, "UnitRequired", () => L["UnitRequired"]);
        }

        // Required: Status
        EditValidation.ValidateRequiredCollection("Status", SelectedStatusMasterData, "StatusRequired", () => L["StatusRequired"]);

        return EditValidation.IsValid;
    }

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

    private async Task DeleteFileAsync(DocumentFileWithNavigationPropertiesDto file)
    {
        try
        {
            if (!await UiMessageService.Confirm(L["DeleteConfirmationMessage"],
            options: new Action<UiMessageOptions>(options => options.ConfirmButtonText = L["Confirm"])))
            {
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);

            if (!string.IsNullOrEmpty(file.DocumentFile.Path))
            {
                try
                {
                    await BlobContainer.DeleteAsync(file.DocumentFile.Path);
                    Logger.LogInformation($"File deleted from MinIO: {file.DocumentFile.Path}");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, $"Failed to delete file from MinIO: {file.DocumentFile.Path}");
                }
            }

            await DocumentFilesAppService.DeleteAsync(file.DocumentFile.Id);

            await LoadDocumentFilesAsync();
            
            await LoadPdfUrlAsync();

            await UiMessageService.Success(L["SuccessfullyDeleted"],
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Error deleting file. FileId: {file.DocumentFile.Id}");
            await HandleErrorAsync(ex);
        }
        finally
        {
            await BlockUiService.UnBlock();
        }
    }

    private async Task DeleteDocumentAsync()
    {
        try
        {   
            
            if (CurrentDocument == null)
            {
                await UiMessageService.Warn(L["NoDataAvailable"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            if (!await UiMessageService.Confirm(L["DeleteConfirmationMessage"],
            options: new Action<UiMessageOptions>(options => options.ConfirmButtonText = L["Confirm"])))
            {
                return;
            }
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            await DocumentsAppService.DeleteAsync(CurrentDocument.Document.Id);
            await UiMessageService.Success(L["SuccessfullyDeleted"],
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            var sourceTypeParam = SourceType.HasValue ? $"?sourceType={SourceType.Value}" : "";
            NavigationManager.NavigateTo("/manage-documents" + sourceTypeParam);
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
    // Check if file is PDF based on extension
    private bool IsPdfFileExtension(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;
        
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension == ".pdf";
    }

    // Load PDF URL for viewer
    private async Task LoadPdfUrlAsync()
    {
        PdfFileUrl = "https://pdfobject.com/pdf/sample.pdf";;
        IsPdfFile = false;

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
            IsPdfFile = true;
            
            // Get file bytes from MinIO
            var fileBytes = await BlobContainer.GetAllBytesAsync(firstFile.DocumentFile.Path);
            
            // Create data URL for PDF
            var base64 = Convert.ToBase64String(fileBytes);
            PdfFileUrl = $"data:application/pdf;base64,{base64}";
            
            Logger.LogInformation($"PDF URL created for file: {firstFile.DocumentFile.Name}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Error loading PDF URL for file: {firstFile.DocumentFile.Path}");
            IsPdfFile = false;
            PdfFileUrl = "https://pdfobject.com/pdf/sample.pdf";;
        }
    }


    private string GenerateStorageNumber()
    {
        return $"{DateTime.Now.ToString("yyyyMMdd")}-{DateTime.Now.ToString("HHmmss")}";
    }

    private async Task LoadDocumentHistoriesAsync()
    {
        try
        {
            Logger.LogInformation($"LoadDocumentHistoriesAsync called. DocumentId: {DocumentId}");

            if (DocumentId == Guid.Empty)
            {
                Logger.LogWarning("LoadDocumentHistoriesAsync: DocumentId is Empty");
                return;
            }

            var result = await DocumentHistoriesAppService.GetHistoryByDocumentIdAsync(
                new GetDocumentHistoriesInput
                {
                    DocumentId = DocumentId,
                    SkipCount = (DocumentHistoriesCurrentPage - 1) * DocumentHistoriesPageSize,
                    MaxResultCount = DocumentHistoriesPageSize
                });

            DocumentHistoriesList = result.Items;
            DocumentHistoriesTotalCount = (int)result.TotalCount;
            DocumentHistoriesCurrentPage = 1;
            Logger.LogInformation($"LoadDocumentHistoriesAsync: Loaded {DocumentHistoriesList.Count} history items, TotalCount: {DocumentHistoriesTotalCount}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Error loading document histories for DocumentId: {DocumentId}");
            DocumentHistoriesList = new List<DocumentHistoryWithNavigationPropertiesDto>();
            DocumentHistoriesTotalCount = 0;
        }
    }

    /// <summary>
    /// Handle DataGrid read event for document histories pagination
    /// </summary>
    private async Task OnDocumentHistoriesReadAsync(DataGridReadDataEventArgs<DocumentHistoryWithNavigationPropertiesDto> e)
    {
        try
        {
            Logger.LogInformation($"OnDocumentHistoriesReadAsync called. Page: {e.Page}, PageSize: {DocumentHistoriesPageSize}");

            if (DocumentId == Guid.Empty)
            {
                Logger.LogWarning("OnDocumentHistoriesReadAsync: DocumentId is Empty");
                return;
            }

            DocumentHistoriesCurrentPage = e.Page;
            var skipCount = (e.Page - 1) * DocumentHistoriesPageSize;

            var result = await DocumentHistoriesAppService.GetHistoryByDocumentIdAsync(
                new GetDocumentHistoriesInput
                {
                    DocumentId = DocumentId,
                    SkipCount = skipCount,
                    MaxResultCount = DocumentHistoriesPageSize
                });

            DocumentHistoriesList = result.Items;
            DocumentHistoriesTotalCount = (int)result.TotalCount;

            await InvokeAsync(StateHasChanged);
            
            Logger.LogInformation($"OnDocumentHistoriesReadAsync: Loaded {DocumentHistoriesList.Count} history items, TotalCount: {DocumentHistoriesTotalCount}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Error in OnDocumentHistoriesReadAsync for DocumentId: {DocumentId}");
            DocumentHistoriesList = new List<DocumentHistoryWithNavigationPropertiesDto>();
            DocumentHistoriesTotalCount = 0;
        }
    }
}
