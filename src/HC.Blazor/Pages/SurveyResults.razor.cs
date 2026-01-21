using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Blazorise;
using Volo.Abp.AspNetCore.Components.Web.Theming.PageToolbars;
using HC.SurveyResults;
using HC.SurveyLocations;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace HC.Blazor.Pages;

public partial class SurveyResults
{
    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();
    protected PageToolbar Toolbar { get; } = new PageToolbar();

    protected SurveyResultStatisticsDto? Statistics { get; set; } = null;

    protected bool IsLoading { get; set; } = true;
    protected bool HasError { get; set; } = false;
    protected List<SurveyLocationDto> SurveyLocations { get; set; } = new();
    protected Guid? SelectedSurveyLocationId { get; set; }
    protected string pieChartId { get; set; } = string.Empty;
    protected string barChartId { get; set; } = string.Empty;
    protected Dictionary<int, int> RatingDistribution { get; set; } = new();
    protected Dictionary<string, double> CriteriaAverageRatings { get; set; } = new();

    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;
    [Inject] private ILogger<SurveyResults> _logger { get; set; } = null!;

    protected List<string> RatingDistributionLabels => new List<string>
    {
        L["0StarRating"],
        L["1StarRating"],
        L["2StarRating"],
        L["3StarRating"],
        L["4StarRating"],
        L["5StarRating"]
    };
    protected List<double> RatingDistributionData 
    {
        get
        {
            if (Statistics?.RatingDistribution == null) return new();
            var stats = Statistics.RatingDistribution;
            return new List<double>
            {
                (double)(stats.GetValueOrDefault(0)),
                (double)(stats.GetValueOrDefault(1)),
                (double)(stats.GetValueOrDefault(2)),
                (double)(stats.GetValueOrDefault(3)),
                (double)(stats.GetValueOrDefault(4)),
                (double)(stats.GetValueOrDefault(5))
            };
        }
    }
    protected List<string> RatingDistributionColors => new List<string>
    {
       "#e74c3c", "#f39c12", "#9b59b6",  "#3498db", "#2ecc71", 
        "#1abc9c", "#34495e", "#e67e22", "#95a5a6", "#16a085"
    };

    protected List<string> CriteriaAverageRatingsLabels => Statistics?.CriteriaAverageRatings?.Keys.ToList() ?? new();
    protected List<double> CriteriaAverageRatingsData => Statistics?.CriteriaAverageRatings?.Values.ToList() ?? new();
    protected List<string> CriteriaAverageRatingsColors => Statistics?.CriteriaAverageRatings?.Keys.Count() > 0 ?
    Enumerable.Repeat("#3498db", Statistics.CriteriaAverageRatings.Keys.Count()).ToList() : new();
    protected override async Task OnInitializedAsync()
    {
        pieChartId = $"ratingDistributionChart-{Guid.NewGuid()}";
        barChartId = $"criteriaAverageRatingChart-{Guid.NewGuid()}";
        await SetToolbarItemsAsync();
        await SetBreadcrumbItemsAsync();
        await LoadSurveyLocationsAsync();
        IsLoading = false;
    }

    protected virtual ValueTask SetBreadcrumbItemsAsync()
    {
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["Menu:SurveyResults"]));
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["ExportToExcel"], async () =>
        {
            await DownloadAsExcelAsync();
        }, IconName.Download);

        return ValueTask.CompletedTask;
    }

    protected virtual async Task LoadSurveyLocationsAsync()
    {
        try
        {
            var result = await SurveyLocationsAppService.GetListAsync(new GetSurveyLocationsInput
            {
                MaxResultCount = 1000
            });
            SurveyLocations = result.Items.ToList();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    protected virtual async Task OnSurveyLocationChanged(Guid? locationId)
    {
        SelectedSurveyLocationId = locationId;
        await LoadChartsAsync();
    }

    protected virtual async Task LoadChartsAsync()
    {
        try
        {
            IsLoading = true;
            HasError = false;
            Statistics = await SurveyResultsAppService.GetStatisticsByLocationAsync(SelectedSurveyLocationId);
        }
        catch (Exception ex)
        {
            HasError = true;
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    protected virtual async Task DownloadAsExcelAsync()
    {
        try
        {
            var filter = new SurveyResultExcelDownloadDto
            {
                FilterText = string.Empty
            };

            var remoteStreamContent = await SurveyResultsAppService.GetListAsExcelFileAsync(filter);
            var fileStream = remoteStreamContent.GetStream();
            var fileName = $"survey_results_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            await JSRuntime.InvokeVoidAsync("downloadFile", fileName, Convert.ToBase64String(((System.IO.MemoryStream)fileStream).ToArray()));
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }
}
