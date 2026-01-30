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

namespace HC.Blazor.Pages;

public partial class ViewDocumentDetail
{
    [Parameter] public Guid DocumentId { get; set; }

    [SupplyParameterFromQuery(Name = "id")]
    public Guid? DocumentIdQuery { get; set; }

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
    private string? PdfFileUrl { get; set; }
    private Modal PdfViewerModal { get; set; } = new Modal();

    protected override async Task OnInitializedAsync()
    {
        await SetBreadcrumbItemsAsync();
        await SetToolbarItemsAsync();
    }

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

            await InvokeAsync(StateHasChanged);
        }
    }

    protected virtual ValueTask SetBreadcrumbItemsAsync()
    {
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["DocumentAssignments"], "/document-assignments"));
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["ViewDocumentDetail"]));
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["Back"], () =>
        {
            NavigationManager.NavigateTo("/document-assignments");
            return  Task.CompletedTask;
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
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
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

    private async Task<LookupDto<Guid>?> GetUnitByIdAsync(Guid id)
    {
        var result = await DocumentsAppService.GetUnitLookupAsync(new LookupRequestDto { Filter = "" });
        return result.Items.FirstOrDefault(x => x.Id == id);
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

        if (DocumentData.UnitId != default && DocumentData.UnitId.HasValue)
        {
            var unitData = await GetUnitByIdAsync(DocumentData.UnitId.Value);
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

    private async Task ViewFileAsync(DocumentFileDto file)
    {
        try
        {
            if (string.IsNullOrEmpty(file.Path))
            {
                await UiMessageService.Warn(L["FileNotFound"]);
                return;
            }
            var fileStream = await BlobContainer.GetAllBytesOrNullAsync(file.Path ?? "");
            if (fileStream != null)
            {
                var base64 = Convert.ToBase64String(fileStream);
                PdfFileUrl = $"data:application/pdf;base64,{base64}";
                await PdfViewerModal.Show();
            }
            else
            {
                await UiMessageService.Warn(L["FileNotFound"]);
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task DownloadFileAsync(DocumentFileDto file)
    {
        try
        {
            if (string.IsNullOrEmpty(file.Path))
            {
                await UiMessageService.Warn(L["FileNotFound"]);
                return;
            }
            var fileStream = await BlobContainer.GetAllBytesOrNullAsync(file.Path);
            if (fileStream != null)
            {
                var base64 = Convert.ToBase64String(fileStream);
                var fileName = file.Name ?? "document.pdf";
                await JSRuntime.InvokeVoidAsync("downloadFile", fileName, "application/pdf", base64);
            }
            else
            {
                await UiMessageService.Warn(L["FileNotFound"]);
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }
}
