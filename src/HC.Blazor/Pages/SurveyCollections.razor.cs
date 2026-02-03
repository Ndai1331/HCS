using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Blazorise;
using HC.SurveySessions;
using HC.SurveyLocations;
using HC.SurveyCriterias;
using HC.SurveyResults;
using HC.SurveyFiles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Volo.Abp.Http.Client;
using Volo.Abp.AspNetCore.Components.Messages;
using Volo.Abp.AspNetCore.Components.BlockUi;
using Microsoft.JSInterop;

namespace HC.Blazor.Pages;

public partial class SurveyCollections
{
    [Parameter]
    public Guid SurveyLocationId { get; set; }


    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;
    [Inject]
    private ILogger<SurveyCollections> _logger { get; set; } = null!;


    [Inject]
    private IRemoteServiceConfigurationProvider RemoteServiceConfigurationProvider { get; set; } = default!;

    protected string PageTitle => $"{L["SurveyCollections:Title"]}: {SurveyLocation?.Name ?? string.Empty}";

    protected bool IsLoading { get; set; } = true;
    protected bool IsShowThankYouMessage { get; set; } = false;

    protected Modal CreateSurveySessionModal { get; set; } = default!;

    // Data
    protected SurveyLocationDto? SurveyLocation { get; set; }
    protected List<SurveyCriteriaWithNavigationPropertiesDto> SurveyCriterias { get; set; } = new();
    protected SurveySessionDto? CurrentSurveySession { get; set; }
    // Form data
    protected SurveySessionCreateDto NewSurveySession { get; set; } = new();
    protected List<SurveyFileCreateDto> NewSurveyFiles { get; set; } = new();

    // Step data
    protected Dictionary<Guid, IFileEntry> CriteriaFiles { get; set; } = new();
    protected Dictionary<Guid, string> UploadedFileNames { get; set; } = new();
    protected string ApiBaseUrl { get; set; } = string.Empty;
    protected  List<SurveyResultCreateDto> NewSurveyResults { get; set; } = new();
    protected FilePicker FilePicker { get; set; } = default!;
    public SurveyCollections()
    {
        NewSurveySession = new SurveySessionCreateDto
        {
            SurveyTime = DateTime.Now,
            DeviceType = DeviceType.DESKTOP // Default device type
        };
    }

    protected override async Task OnInitializedAsync()
    {
        var blobFilesService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("BlobFiles");
        ApiBaseUrl = blobFilesService?.BaseUrl?.EnsureEndsWith('/') ?? string.Empty;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await LoadSurveyLocationAsync();
            await LoadSurveyCriteriasAsync();

            IsLoading = false;

            await InvokeAsync(StateHasChanged);
        }
    }

    protected virtual async Task LoadSurveyLocationAsync()
    {
        try
        {
            SurveyLocation = await SurveyLocationsAppService.GetPublicSurveyLocationAsync(SurveyLocationId);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
            SurveyLocation = null;
        }
    }

    protected virtual async Task LoadSurveyCriteriasAsync()
    {
        try
        {
            if (SurveyLocation == null)
            {
                _logger.LogWarning("SurveyLocation is null, skipping LoadSurveyCriteriasAsync");
                return;
            }

            _logger.LogInformation($"Loading survey criterias for LocationId: {SurveyLocationId}");

            var criterias = await SurveyCriteriasAppService.GetPublicSurveyCriteriasByLocationAsync(SurveyLocationId);
            _logger.LogInformation($"Loaded {criterias?.Count ?? 0} criterias");

            if (criterias == null || !criterias.Any())
            {
                await UiMessageService.Warn(L["SurveyCollections:NoCriteriaFound"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                SurveyCriterias = new List<SurveyCriteriaWithNavigationPropertiesDto>();
                return;
            }

            SurveyCriterias = criterias.Select(c => new SurveyCriteriaWithNavigationPropertiesDto
            {
                SurveyCriteria = c,
                SurveyLocation = SurveyLocation
            }).ToList();

            foreach (var criteria in SurveyCriterias)
            {
                NewSurveyResults.Add(new SurveyResultCreateDto
                {
                    SurveyCriteriaId = criteria.SurveyCriteria.Id,
                    Rating = 5
                });
            }

            _logger.LogInformation($"NewSurveyResults: {NewSurveyResults.Count}");

            _logger.LogInformation($"Mapped to {SurveyCriterias.Count} SurveyCriteriaWithNavigationPropertiesDto objects");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading survey criterias");
            await HandleErrorAsync(ex);
            SurveyCriterias = new List<SurveyCriteriaWithNavigationPropertiesDto>();
        }
    }

    protected virtual async Task CreateSurveySessionAsync()
    {
        await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
        try
        {
            if (string.IsNullOrWhiteSpace(NewSurveySession.FullName))
            {
                await UiMessageService.Error(L["FullNameIsRequired"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            if (string.IsNullOrWhiteSpace(NewSurveySession.PhoneNumber))
            {
                await UiMessageService.Error(L["PhoneNumberIsRequired"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            NewSurveySession.SurveyLocationId = SurveyLocationId;
            NewSurveySession.SessionDisplay = GenerateSessionDisplay();

            CurrentSurveySession = await SurveySessionsAppService.CreatePublicSurveySessionAsync(NewSurveySession);
            if (CurrentSurveySession == null)
            {
                await UiMessageService.Error(L["SurveyCollections:FailedToCreateSurveySession"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                throw new Exception(L["SurveyCollections:FailedToCreateSurveySession"]);
            }

            foreach (var surveyFile in NewSurveyFiles)
            {
                surveyFile.SurveySessionId = CurrentSurveySession.Id;
                var createdSurveyFile = await SurveyFilesAppService.CreatePublicSurveyFileAsync(surveyFile);

                _logger.LogInformation($"Created survey file: {createdSurveyFile.Id}");

                if (createdSurveyFile == null)
                {
                    await UiMessageService.Error(L["SurveyCollections:FailedToCreateSurveyFile"],
                    options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                    throw new Exception(L["SurveyCollections:FailedToCreateSurveyFile"]);
                }
            }

            _logger.LogInformation($"Creating {NewSurveyResults.Count} survey results");

            foreach (var surveyResult in NewSurveyResults)
            {
                
                surveyResult.SurveySessionId = CurrentSurveySession.Id;
            }

            var surveyResults = await SurveyResultsAppService.CreatePublicSurveyResultsAsync(NewSurveyResults);
            if (surveyResults.Count > 0)
            {
                IsShowThankYouMessage = true;
                await UiMessageService.Success(L["SurveyCollections:SurveySessionCreatedSuccessfully"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                await CloseCreateSurveySessionModalAsync();
            }
            else
            {
                await UiMessageService.Error(L["SurveyCollections:FailedToCreateSurveySession"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                throw new Exception(L["SurveyCollections:FailedToCreateSurveySession"]);
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
            _logger.LogError(ex, "Error creating survey session");
        } finally 
        {
            await InvokeAsync(BlockUiService.UnBlock);
        }
    }

    protected virtual string GenerateSessionDisplay()
    {
        return $"{NewSurveySession.FullName}_{NewSurveySession.PhoneNumber}_{SurveyLocationId}_{NewSurveySession.SurveyTime:ddMMyyyyHHmm}";
    }


    protected virtual int GetRatingForCriteria(Guid criteriaId)
    {
        _logger.LogInformation($"NewSurveyResults Getting rating for criteria: {NewSurveyResults.Count}");
        return NewSurveyResults.FirstOrDefault(x => x.SurveyCriteriaId == criteriaId)?.Rating ?? 1;
    }

    protected virtual async Task SetRatingForCriteria(Guid criteriaId, int rating)
    {
        _logger.LogInformation($"NewSurveyResults Setting rating for criteria: {NewSurveyResults.Count} to {rating}");
        var surveyResult = NewSurveyResults.FirstOrDefault(x => x.SurveyCriteriaId == criteriaId);
        if (surveyResult != null)
        {
            surveyResult.Rating = rating;
        }
        else
            await UiMessageService.Error(L["SurveyCollections:FailedToSetRatingForCriteria"],
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
    }

    protected virtual string GetUploadedFileName()
    {
        return NewSurveyFiles != null && NewSurveyFiles.Count > 0 ? NewSurveyFiles.Select(file => file.FileName).Aggregate((a, b) => $"{a}, {b}") : string.Empty;
    }

    protected virtual async Task OnFileChangedAsync(FileChangedEventArgs files)
    {
        try
        {
            if (files.Files?.Any() == true)
            {
                NewSurveyFiles.AddRange(files.Files.Select(file => new SurveyFileCreateDto
                {
                    // SurveySessionId = CurrentSurveySession.Id,
                    FilePath = $"survey-files/{DateTime.Now:yyyyMMddHHmmss}/{file.Name}",
                    UploaderType = UploaderType.PATIENT,
                    FileName = file.Name,
                    FileSize = (int)file.Size,
                    MimeType = file.Type,
                    FileType = System.IO.Path.GetExtension(file.Name).TrimStart('.')
                }));
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    protected virtual void ClearFiles()
    {
        NewSurveyFiles.Clear();
        UploadedFileNames.Clear();
        FilePicker.Clear();
        StateHasChanged();
    }

    protected virtual async Task ShowModalCreateSurveySessionAsync()
    {
        string type = await JSRuntime.InvokeAsync<string>("getDeviceType");

        DeviceType deviceType;

        if (!Enum.TryParse<DeviceType>(type, true, out deviceType))
        {
            deviceType = DeviceType.DESKTOP; // default fallback
        }
        ClearFiles();

        NewSurveySession = new SurveySessionCreateDto
        {
            SurveyTime = DateTime.Now,
            SurveyLocationId = SurveyLocationId,
            DeviceType = deviceType
        };

        await InvokeAsync(CreateSurveySessionModal.Show);
    }

    protected virtual async Task CloseCreateSurveySessionModalAsync()
    {
        await InvokeAsync(CreateSurveySessionModal.Hide);
    }

    protected virtual void OnReloadPage()
    {
        NavigationManager.NavigateTo($"/survey-collections/{SurveyLocationId}", forceLoad: true);
    }
}