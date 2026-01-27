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
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Volo.Abp.BlobStoring;
using Volo.Abp.Application.Dtos;
using Blazorise.PdfViewer;
using Volo.Abp.AspNetCore.Components.Web.Theming.PageToolbars;

namespace HC.Blazor.Pages;

public partial class DocumentDetail : HCComponentBase
{
    [Parameter] public Guid DocumentId { get; set; }

    [SupplyParameterFromQuery(Name = "id")]
    public Guid? DocumentIdQuery { get; set; }

    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems { get; } = new();

    protected string PageTitle => IsEditMode
        ? (CurrentDocument?.Document is null ? L["Documents"] : L["EditDocument"])
        : L["NewDocument"];

    protected bool IsLoading { get; set; }
    protected bool IsSaving { get; set; }
    protected bool IsEditMode => DocumentId != Guid.Empty;
    protected bool IsViewMode { get; set; } = false;
    protected DocumentWithNavigationPropertiesDto? CurrentDocument { get; set; }
    protected PageToolbar Toolbar { get; } = new PageToolbar();
    private bool CanEditDocument { get; set; }
    private bool CanCreateDocument { get; set; }
    private bool CanDeleteDocumentFile { get; set; }


    // Document data
    private DocumentCreateDto? DocumentCreateData { get; set; }
    private DocumentUpdateDto? DocumentUpdateData { get; set; }

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

    // File upload
    private IFileEntry? SelectedFile { get; set; }
    private string UploadedFilePath { get; set; } = string.Empty;
    private string UploadedFileHash { get; set; } = string.Empty;
    private bool IsUploading { get; set; }
    private int FilePickerProgress { get; set; }

    private IReadOnlyList<DocumentFileWithNavigationPropertiesDto> DocumentFilesList { get; set; } = new List<DocumentFileWithNavigationPropertiesDto>();

    // PDF viewer
    private string? PdfFileUrl { get; set; } = "https://pdfobject.com/pdf/sample.pdf";
    private bool IsPdfFile { get; set; }

    // PDF Viewer Modal
    private Modal? PdfViewerModal { get; set; }

    // DatePicker refs
    private DatePicker<DateTime>? EditCompletedTimeDatePicker { get; set; }
    private DatePicker<DateTime>? CreateCompletedTimeDatePicker { get; set; }

    private Guid _loadedDocumentId;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SetPermissionsAsync();
            await SetToolbarItemsAsync();
            await InvokeAsync(StateHasChanged);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        
        if (DocumentId == Guid.Empty && DocumentIdQuery.HasValue)
        {
            DocumentId = DocumentIdQuery.Value;
        }
        if (DocumentId == Guid.Empty)
        {
            IsLoading = true;
            try
            {
                InitializeCreateMode();
                
                BreadcrumbItems.Clear();
                BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["Documents"], "/documents"));
                BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(IsEditMode ? L["Details"] : L["NewDocument"]));

                await LoadLookupDataAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }
        else
        {
            if (_loadedDocumentId == DocumentId)
            {
                Logger.LogInformation($"OnParametersSetAsync: Document already loaded, skipping. DocumentId: {DocumentId}");
                BreadcrumbItems.Clear();
                BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["Documents"], "/documents"));
                BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(IsEditMode ? L["Details"] : L["NewDocument"]));
                await LoadLookupDataAsync();
                await InvokeAsync(StateHasChanged);
                return;
            }

            BreadcrumbItems.Clear();
            BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["Documents"], "/documents"));
            BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(IsEditMode ? L["Details"] : L["NewDocument"]));

            // Set loading state before loading data
            IsLoading = true;
            try
            {
                // Load lookup data first (collections must be loaded before setting selected values)
                await LoadLookupDataAsync();

                Logger.LogInformation($"OnParametersSetAsync: Loading document. DocumentId: {DocumentId}");
                _loadedDocumentId = DocumentId;
                await LoadDocumentAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task SetPermissionsAsync()
    {
        CanCreateDocument = await HasRoleHelper.HasRoleAsync(AuthorizationService, HCPermissions.Documents.Create);
        CanEditDocument = await HasRoleHelper.HasRoleAsync(AuthorizationService, HCPermissions.Documents.Edit);
        CanDeleteDocumentFile = await HasRoleHelper.HasRoleAsync(AuthorizationService, HCPermissions.DocumentFiles.Delete);
    }

    private void InitializeCreateMode()
    {
        DocumentCreateData = new DocumentCreateDto
        {
            CompletedTime = DateTime.Now
        };
        DocumentUpdateData = null;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {   
        Toolbar.AddButton(L["Back"], () =>
        {
            NavigationManager.NavigateTo("/documents");
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

                // Load document files
                Logger.LogInformation($"LoadDocumentAsync: Calling LoadDocumentFilesAsync");
                await LoadDocumentFilesAsync();
                
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
        // IsLoading is managed in OnParametersSetAsync
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

    // MasterData lookup by Type using MasterDatasAppService
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
            if (IsEditMode && DocumentUpdateData != null)
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
            if (IsEditMode && DocumentUpdateData != null)
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
            if (IsEditMode && DocumentUpdateData != null)
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
            if (IsEditMode && DocumentUpdateData != null)
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
            if (IsEditMode && DocumentUpdateData != null)
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
            if (IsEditMode && DocumentUpdateData != null)
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
            if (IsEditMode && DocumentUpdateData != null)
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
            if (IsEditMode && DocumentUpdateData != null)
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
            if (IsEditMode && DocumentUpdateData != null)
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

    private async Task OnFileChanged(FileChangedEventArgs e)
    {
        if (e.Files != null && e.Files.Any())
        {
            var file = e.Files.First();
            await UploadFileAsync(file);
        }

       
    }

    private async Task OnFileUpload(FileUploadEventArgs e)
    {
        await UploadFileAsync(e.File);
    }

    private async Task UploadFileAsync(IFileEntry file)
    {
        try
        {
            IsUploading = true;
            SelectedFile = file;
            FilePickerProgress = 0;

            using var memoryStream = new MemoryStream();
            await file.OpenReadStream(long.MaxValue).CopyToAsync(memoryStream);
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
                PdfFileUrl = "https://pdfobject.com/pdf/sample.pdf";;
            }
            await UiMessageService.Success(L["FileUploadedSuccessfully"]);

        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
            UploadedFilePath = string.Empty;
            FilePickerProgress = 0;
            PdfFileUrl = "https://pdfobject.com/pdf/sample.pdf";;
            IsPdfFile = false;
        }
        finally
        {
            IsUploading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task OnSave()
    {
        IsSaving = true;
        try
        {
            if (IsEditMode && DocumentUpdateData != null)
            {
                if (!ValidateEditDocument())
                {
                    await UiMessageService.Warn(L[EditDocumentValidationErrorKey ?? "ValidationError"]);
                    await InvokeAsync(StateHasChanged);
                    return;
                }
            }
            else if (DocumentCreateData != null)
            {
                if (!ValidateCreateDocument())
                {
                    await UiMessageService.Warn(L[CreateDocumentValidationErrorKey ?? "ValidationError"]);
                    await InvokeAsync(StateHasChanged);
                    return;
                }
            }
            else
            {
                // Should not happen, but handle gracefully
                await UiMessageService.Warn(L["PleaseFillRequiredFields"]);
                return;
            }

            // Validate required file is uploaded for create mode
            if (!IsEditMode && string.IsNullOrEmpty(UploadedFilePath))
            {
                await UiMessageService.Warn(L["PleaseUploadFileFirst"]);
                return;
            }

            DocumentDto savedDocument;

            if (IsEditMode && DocumentUpdateData != null)
            {
                savedDocument = await DocumentsAppService.UpdateAsync(DocumentId, DocumentUpdateData);
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

            await UiMessageService.Success(L["SuccessfullySaved"]);
            NavigationManager.NavigateTo($"/document-detail/{savedDocument.Id}");
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsSaving = false;
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

        // Required: Unit
        CreateValidation.ValidateRequiredCollection("Unit", SelectedUnit, "UnitRequired", () => L["UnitRequired"]);

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

        // Required: Unit
        EditValidation.ValidateRequiredCollection("Unit", SelectedUnit, "UnitRequired", () => L["UnitRequired"]);

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
    }

    private async Task DeleteFileAsync(DocumentFileWithNavigationPropertiesDto file)
    {
        try
        {
            if (!await UiMessageService.Confirm(L["DeleteConfirmationMessage"]))
            {
                return;
            }

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

            await UiMessageService.Success(L["SuccessfullyDeleted"]);
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Error deleting file. FileId: {file.DocumentFile.Id}");
            await HandleErrorAsync(ex);
        }
    }

    private async Task DeleteDocumentAsync()
    {
        try
        {   
            
            if (CurrentDocument == null)
            {
                await UiMessageService.Warn(L["NoDataAvailable"]);
                return;
            }

            if (!await UiMessageService.Confirm(L["DeleteConfirmationMessage"]))
            {
                return;
            }
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            await DocumentsAppService.DeleteAsync(CurrentDocument.Document.Id);
            await UiMessageService.Success(L["SuccessfullyDeleted"]);
            NavigationManager.NavigateTo("/documents");
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
}
