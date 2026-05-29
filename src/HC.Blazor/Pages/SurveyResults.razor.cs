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
using Microsoft.AspNetCore.Components.Web;

namespace HC.Blazor.Pages;

public partial class SurveyResults : IAsyncDisposable
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
    protected string surveyLocationSelectElementId { get; } = $"survey-location-select-{Guid.NewGuid():N}";
    private DotNetObjectReference<SurveyResults>? _surveyLocationSelect2DotNetRef;
    private bool _surveyLocationSelect2Initialized;
    private bool _pendingSurveyLocationSelect2Init;

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
    protected int TotalReviews => Statistics?.TotalReviews ?? 0;
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
            _pendingSurveyLocationSelect2Init = true;
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (_pendingSurveyLocationSelect2Init && SurveyLocations.Count > 0)
        {
            await InitializeSurveyLocationSelect2Async();
            _pendingSurveyLocationSelect2Init = false;
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

    protected virtual async Task OnSurveyLocationNativeChanged(ChangeEventArgs args)
    {
        var value = args.Value?.ToString();
        var parsed = Guid.TryParse(value, out var locationId) ? locationId : (Guid?)null;
        await OnSurveyLocationChanged(parsed);
    }

    [JSInvokable]
    public async Task OnSurveyLocationSelect2Changed(string? value)
    {
        var parsed = Guid.TryParse(value, out var locationId) ? locationId : (Guid?)null;
        if (parsed == SelectedSurveyLocationId)
        {
            return;
        }

        await OnSurveyLocationChanged(parsed);
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

    private async Task InitializeSurveyLocationSelect2Async()
    {
        _surveyLocationSelect2DotNetRef ??= DotNetObjectReference.Create(this);
        await JSRuntime.InvokeVoidAsync("HcSurveyResultsLocationSelect2.init", surveyLocationSelectElementId, _surveyLocationSelect2DotNetRef);
        _surveyLocationSelect2Initialized = true;
        await JSRuntime.InvokeVoidAsync("HcSurveyResultsLocationSelect2.setValue", surveyLocationSelectElementId, SelectedSurveyLocationId?.ToString());
    }

    public async ValueTask DisposeAsync()
    {
        if (_surveyLocationSelect2Initialized)
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("HcSurveyResultsLocationSelect2.destroy", surveyLocationSelectElementId);
            }
            catch (JSDisconnectedException)
            {
                // Browser circuit was disconnected.
            }
            catch (TaskCanceledException)
            {
                // Ignore cancellation during shutdown.
            }
        }

        _surveyLocationSelect2DotNetRef?.Dispose();
    }
}
