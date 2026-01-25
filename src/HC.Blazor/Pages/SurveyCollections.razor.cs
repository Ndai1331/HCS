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

namespace HC.Blazor.Pages;

public partial class SurveyCollections
{
    [Parameter]
    public Guid SurveyLocationId { get; set; }

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

    // Step data
    protected Dictionary<Guid, IFileEntry> CriteriaFiles { get; set; } = new();
    protected Dictionary<Guid, string> UploadedFileNames { get; set; } = new();
    protected string ApiBaseUrl { get; set; } = string.Empty;
    protected  List<SurveyResultCreateDto> NewSurveyResults { get; set; } = new();

    public SurveyCollections()
    {
        NewSurveySession = new SurveySessionCreateDto
        {
            SurveyTime = DateTime.Now,
            DeviceType = DeviceType.WEB // Default device type
        };
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadSurveyLocationAsync();
        await LoadSurveyCriteriasAsync();
        var blobFilesService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("BlobFiles");
        ApiBaseUrl = blobFilesService?.BaseUrl?.EnsureEndsWith('/') ?? string.Empty;
        IsLoading = false;
    }

    protected override async Task OnParametersSetAsync()
    {
        await LoadSurveyLocationAsync();
        await LoadSurveyCriteriasAsync();
        IsLoading = false;
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
                await UiMessageService.Warn(L["SurveyCollections:NoCriteriaFound"]);
                SurveyCriterias = new List<SurveyCriteriaWithNavigationPropertiesDto>();
                return;
            }

            SurveyCriterias = criterias.Select(c => new SurveyCriteriaWithNavigationPropertiesDto
            {
                SurveyCriteria = c,
                SurveyLocation = SurveyLocation
            }).ToList();

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
        try
        {
            if (string.IsNullOrWhiteSpace(NewSurveySession.FullName))
            {
                await UiMessageService.Error(L["FullNameIsRequired"]);
                return;
            }

            if (string.IsNullOrWhiteSpace(NewSurveySession.PhoneNumber))
            {
                await UiMessageService.Error(L["PhoneNumberIsRequired"]);
                return;
            }

            NewSurveySession.SurveyLocationId = SurveyLocationId;
            NewSurveySession.SessionDisplay = GenerateSessionDisplay();

            CurrentSurveySession = await SurveySessionsAppService.CreatePublicSurveySessionAsync(NewSurveySession);

            foreach (var surveyResult in NewSurveyResults)
            {
                surveyResult.SurveySessionId = CurrentSurveySession.Id;
            }

            var surveyResults = await SurveyResultsAppService.CreatePublicSurveyResultsAsync(NewSurveyResults);
            if (surveyResults.Count > 0)
            {
                IsShowThankYouMessage = true;
                await UiMessageService.Success(L["SurveyCollections:SurveySessionCreatedSuccessfully"]);
                await CloseCreateSurveySessionModalAsync();
            }
            else
            {
                await UiMessageService.Error(L["SurveyCollections:FailedToCreateSurveySession"]);
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    protected virtual string GenerateSessionDisplay()
    {
        return $"{NewSurveySession.FullName}_{NewSurveySession.PhoneNumber}_{SurveyLocationId}_{NewSurveySession.SurveyTime:ddMMyyyyHHmm}";
    }


    protected virtual int GetRatingForCriteria(Guid criteriaId)
    {
        return NewSurveyResults.FirstOrDefault(x => x.SurveyCriteriaId == criteriaId)?.Rating ?? 1;
    }

    protected virtual void SetRatingForCriteria(Guid criteriaId, int rating)
    {
        var surveyResult = NewSurveyResults.FirstOrDefault(x => x.SurveyCriteriaId == criteriaId);
        if (surveyResult != null)
        {
            surveyResult.Rating = rating;
        }
        else
        {
            NewSurveyResults.Add(new SurveyResultCreateDto
            {
                Rating = rating,
                SurveyCriteriaId = criteriaId,
                SurveySessionId = Guid.Empty
            });
        }
    }

    protected virtual string GetUploadedFileNameForCriteria(Guid criteriaId)
    {
        return UploadedFileNames.GetValueOrDefault(criteriaId, string.Empty);
    }

    protected virtual async Task OnFileChangedAsync(Guid criteriaId, FileChangedEventArgs files)
    {
        try
        {
            if (files.Files?.Any() == true)
            {
                var file = files.Files.First();
                CriteriaFiles[criteriaId] = file;
                UploadedFileNames[criteriaId] = file.Name;
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    protected virtual void ClearFileForCriteria(Guid criteriaId)
    {
        CriteriaFiles.Remove(criteriaId);
        UploadedFileNames.Remove(criteriaId);
        StateHasChanged();
    }


    protected virtual async Task UploadFileForCriteriaAsync(Guid criteriaId, IFileEntry file)
    {
        if (CurrentSurveySession == null)
        {
            return;
        }
        
        try
        {
            // TODO: Implement actual file upload to storage service
            // For now, we just save file metadata
            
            var surveyFile = new SurveyFileCreateDto
            {
                SurveySessionId = CurrentSurveySession.Id,
                UploaderType = UploaderType.PATIENT,
                FileName = file.Name,
                FilePath = $"survey-files/{CurrentSurveySession.Id}/{criteriaId}/{file.Name}",
                FileSize = (int)file.Size,
                MimeType = file.Type,
                FileType = System.IO.Path.GetExtension(file.Name).TrimStart('.')
            };

            _logger.LogInformation($"Creating survey file: SessionId={surveyFile.SurveySessionId}, FileName={surveyFile.FileName}, Size={surveyFile.FileSize}");

            await SurveyFilesAppService.CreatePublicSurveyFileAsync(surveyFile);
            _logger.LogInformation("Survey file created successfully");
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    protected virtual async Task ShowModalCreateSurveySessionAsync()
    {
        NewSurveySession = new SurveySessionCreateDto
        {
            SurveyTime = DateTime.Now,
            SurveyLocationId = SurveyLocationId
        };

        await InvokeAsync(CreateSurveySessionModal.Show);
    }

    protected virtual async Task CloseCreateSurveySessionModalAsync()
    {
        await InvokeAsync(CreateSurveySessionModal.Hide);
    }
}