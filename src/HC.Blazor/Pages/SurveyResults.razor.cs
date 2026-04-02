using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Blazorise;
using Blazorise.DataGrid;
using Volo.Abp.AspNetCore.Components.Web.Theming.PageToolbars;
using HC.SurveyResults;
using HC.SurveyLocations;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Volo.Abp.Http.Client;
using System.Globalization;
using Volo.Abp.Application.Dtos;

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
    protected string SelectedTab { get; set; } = "chart-statistics";
    protected IReadOnlyList<SurveyResultSessionSummaryDto> SurveySessionSummaries { get; set; } = new List<SurveyResultSessionSummaryDto>();
    protected DataGrid<SurveyResultSessionSummaryDto>? SurveySessionSummaryGridRef { get; set; }
    protected int DetailPageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    protected int DetailCurrentPage { get; set; } = 1;
    protected string DetailCurrentSorting { get; set; } = "SurveySession.SurveyTime desc";
    protected int DetailTotalCount { get; set; }
    protected Modal SurveyResultDetailModal { get; set; } = new();
    protected SurveyResultSessionSummaryDto? SelectedSurveySessionSummary { get; set; }
    protected IReadOnlyList<SurveyResultSessionDetailDto> SelectedSurveySessionDetails { get; set; } = new List<SurveyResultSessionDetailDto>();
    protected bool IsDetailModalLoading { get; set; }

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
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            pieChartId = $"ratingDistributionChart-{Guid.NewGuid()}";
            barChartId = $"criteriaAverageRatingChart-{Guid.NewGuid()}";
            await SetToolbarItemsAsync();
            await SetBreadcrumbItemsAsync();
            await LoadSurveyLocationsAsync();
            await LoadStatisticsAsync();
        }
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
                MaxResultCount = 200
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
        await SearchAsync();
    }

    /// <summary>
    /// Search/apply filters - follows same pattern as Projects.razor
    /// </summary>
    protected virtual async Task SearchAsync()
    {
        DetailCurrentPage = 1;
        await LoadStatisticsAsync();
        if (SurveySessionSummaryGridRef != null)
        {
            await SurveySessionSummaryGridRef.Reload();
        }
        else
        {
            await LoadSurveySessionSummariesAsync();
        }
        await InvokeAsync(StateHasChanged);
    }

    protected virtual Task OnSelectedTabChanged(string selectedTab)
    {
        SelectedTab = selectedTab;
        return Task.CompletedTask;
    }

    protected virtual async Task LoadStatisticsAsync()
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

    protected virtual async Task OnSurveyResultGridReadAsync(DataGridReadDataEventArgs<SurveyResultSessionSummaryDto> e)
    {
        DetailCurrentPage = e.Page;
        await LoadSurveySessionSummariesAsync();
        await InvokeAsync(StateHasChanged);
    }

    protected virtual async Task LoadSurveySessionSummariesAsync()
    {
        try
        {
            var summaryResult = await SurveyResultsAppService.GetSessionSummaryListAsync(new GetSurveyResultSessionSummariesInput
            {
                SurveyLocationId = SelectedSurveyLocationId,
                MaxResultCount = DetailPageSize,
                SkipCount = (DetailCurrentPage - 1) * DetailPageSize,
                Sorting = "SurveyTime desc"
            });
            SurveySessionSummaries = summaryResult.Items;
            DetailTotalCount = (int)summaryResult.TotalCount;
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    protected virtual async Task OpenDetailModalAsync(SurveyResultSessionSummaryDto summary)
    {
        SelectedSurveySessionSummary = summary;
        IsDetailModalLoading = true;
        SelectedSurveySessionDetails = new List<SurveyResultSessionDetailDto>();

        await SurveyResultDetailModal.Show();

        try
        {
            var result = await SurveyResultsAppService.GetSessionDetailListAsync(new GetSurveyResultSessionDetailsInput
            {
                SurveyLocationId = SelectedSurveyLocationId,
                SurveySessionId = summary.SurveySessionId
            });

            SelectedSurveySessionDetails = result;
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsDetailModalLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    protected virtual async Task CloseDetailModalAsync()
    {
        await SurveyResultDetailModal.Hide();
    }
    private async Task DownloadAsExcelAsync()
    {
        var token = (await SurveyResultsAppService.GetDownloadTokenAsync()).Token;
        var remoteService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("HC") ?? 
        await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        var culture = CultureInfo.CurrentUICulture.Name ?? CultureInfo.CurrentCulture.Name;
        if (!culture.IsNullOrEmpty())
        {
            culture = "&culture=" + culture;
        }

        await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        NavigationManager.NavigateTo($"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/survey-results/as-excel-file?DownloadToken={token}&SurveyLocationId={SelectedSurveyLocationId}", forceLoad: true);
    }

}
