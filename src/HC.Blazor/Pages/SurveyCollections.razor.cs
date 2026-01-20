using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using Blazorise;
using Blazorise.DataGrid;
using Volo.Abp.BlazoriseUI.Components;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Components.Web.Theming.PageToolbars;
using HC.SurveySessions;
using HC.SurveyLocations;
using HC.SurveyCriterias;
using HC.SurveyResults;
using HC.SurveyFiles;
using HC.Permissions;
using HC.Shared;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Volo.Abp;
using Volo.Abp.Content;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Http.Client;
using Microsoft.Extensions.Logging;

namespace HC.Blazor.Pages;

public partial class SurveyCollections
{
    [Parameter]
    public Guid SurveyLocationId { get; set; }

    [Inject]
    private ILogger<SurveyCollections> _logger { get; set; } = null!;

    protected string PageTitle => $"{L["SurveyCollections:Title"]}: {SurveyLocation?.Name ?? string.Empty}";

    protected bool IsLoading { get; set; } = true;
    protected bool IsSessionCreated { get; set; }
    protected bool IsSubmitting { get; set; }

    protected Steps StepsRef { get; set; } = default!;
    protected string SelectedStep { get; set; } = "step1";
    protected bool NavigationAllowed { get; set; } = true;

    // Data
    protected SurveyLocationDto? SurveyLocation { get; set; }
    protected List<SurveyCriteriaWithNavigationPropertiesDto> SurveyCriterias { get; set; } = new();
    protected SurveySessionDto? CurrentSurveySession { get; set; }

    // Form data
    protected SurveySessionCreateDto NewSurveySession { get; set; } = new();

    // Step data
    protected Dictionary<Guid, int> CriteriaRatings { get; set; } = new();
    protected Dictionary<Guid, IFileEntry> CriteriaFiles { get; set; } = new();
    protected Dictionary<Guid, string> UploadedFileNames { get; set; } = new();

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
            SurveyLocation = await SurveyLocationsAppService.GetAsync(SurveyLocationId);
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
                return;
            }
            
            var criterias = await SurveyCriteriasAppService.GetPublicSurveyCriteriasByLocationAsync(SurveyLocationId);
            
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
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
            SurveyCriterias = new List<SurveyCriteriaWithNavigationPropertiesDto>();
        }
    }

    protected virtual async Task CreateSurveySessionAsync()
    {
        try
        {
            // Simple validation
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
            IsSessionCreated = true;

            // Initialize ratings for all criteria
            foreach (var criteria in SurveyCriterias)
            {
                CriteriaRatings[criteria.SurveyCriteria.Id] = 0;
            }

            await InvokeAsync(StateHasChanged);
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

    protected virtual Task OnSelectedStepChanged(string stepName)
    {
        SelectedStep = stepName;
        return InvokeAsync(StateHasChanged);
    }

    protected virtual bool IsStepCompleted(Guid criteriaId)
    {
        return GetRatingForCriteria(criteriaId) > 0;
    }

    protected virtual bool IsAllStepsCompleted
    {
        get
        {
            return SurveyCriterias.All(c => IsStepCompleted(c.SurveyCriteria.Id));
        }
    }

    protected virtual int GetRatingForCriteria(Guid criteriaId)
    {
        return CriteriaRatings.GetValueOrDefault(criteriaId, 0);
    }

    protected virtual void SetRatingForCriteria(Guid criteriaId, int rating)
    {
        CriteriaRatings[criteriaId] = rating;
        StateHasChanged();
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

    protected virtual string GetImageUrl(string imagePath)
    {
        // Implement logic to get full image URL
        return imagePath; // Placeholder - need to implement proper URL generation
    }

    protected virtual async Task SaveCurrentStepAsync(Guid criteriaId)
    {
        if (IsSubmitting || CurrentSurveySession == null)
        {
            return;
        }

        IsSubmitting = true;

        try
        {
            var rating = GetRatingForCriteria(criteriaId);
            
            if (rating == 0)
            {
                await UiMessageService.Warn(L["SurveyCollections:PleaseSelectRating"]);
                IsSubmitting = false;
                return;
            }

            // Validate that criteria exists in our list
            var criteriaExists = SurveyCriterias.Any(c => c.SurveyCriteria.Id == criteriaId);
            if (!criteriaExists)
            {
                await UiMessageService.Error($"Invalid criteria ID: {criteriaId}");
                IsSubmitting = false;
                return;
            }

            _logger.LogInformation($"Saving survey result for session {CurrentSurveySession.Id} and criteria {criteriaId} with rating {rating}");

            // Create SurveyResult
            var surveyResult = new SurveyResultCreateDto
            {
                Rating = rating,
                SurveyCriteriaId = criteriaId,
                SurveySessionId = CurrentSurveySession.Id
            };

            await SurveyResultsAppService.CreatePublicSurveyResultAsync(surveyResult);

            // Upload file if exists
            if (CriteriaFiles.TryGetValue(criteriaId, out var file))
            {
                await UploadFileForCriteriaAsync(criteriaId, file);
            }

            await UiMessageService.Success(L["SurveyCollections:RatingSaved"]);

            // Move to next step
            var currentIndex = SurveyCriterias.FindIndex(c => c.SurveyCriteria.Id == criteriaId);
            if (currentIndex < SurveyCriterias.Count - 1)
            {
                var nextStepName = $"step{currentIndex + 2}";
                await StepsRef.SelectStep(nextStepName);
            }
            else
            {
                // Last step - move to final step
                await StepsRef.SelectStep("stepFinal");
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    protected virtual async Task CompleteSurveyAsync()
    {
        IsSubmitting = true;
        try
        {
            await UiMessageService.Success(L["SurveyCollections:ThankYouForYourFeedback"]);
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsSubmitting = false;
        }
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

            await SurveyFilesAppService.CreatePublicSurveyFileAsync(surveyFile);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }
}