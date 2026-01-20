using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Blazorise;
using Blazorise.Charts;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.AspNetCore.Components.Web.Theming.PageToolbars;
using HC.SurveyResults;
using HC.SurveyLocations;
using HC.Permissions;
using HC.Shared;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;

namespace HC.Blazor.Pages;

public partial class SurveyResults
{
    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();
    protected PageToolbar Toolbar { get; } = new PageToolbar();

    protected bool IsLoading { get; set; } = true;
    protected List<SurveyLocationDto> SurveyLocations { get; set; } = new();
    protected Guid? SelectedSurveyLocationId { get; set; }

    protected PieChart<int> pieChart { get; set; } = default!;
    protected BarChart<double> barChart { get; set; } = default!;

    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;
    
    protected override async Task OnInitializedAsync()
    {
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
        IsLoading = true;
        try
        {
            // Get survey results statistics
            var stats = await SurveyResultsAppService.GetStatisticsByLocationAsync(SelectedSurveyLocationId);

            // Update PieChart - Rating distribution
            await UpdatePieChartAsync(stats.RatingDistribution);

            // Update BarChart - Criteria average rating
            await UpdateBarChartAsync(stats.CriteriaAverageRatings);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected virtual async Task UpdatePieChartAsync(Dictionary<int, int> ratingDistribution)
    {
        await pieChart.Clear();

        var labels = new List<string>();
        var data = new List<int>();
        var backgroundColors = new List<string>();

        // Define colors for each rating (0-5 stars)
        var colors = new Dictionary<int, string>
        {
            { 0, ChartColor.FromRgba(189, 189, 189, 0.8f) }, // Gray for Unknown
            { 1, ChartColor.FromRgba(244, 67, 54, 0.8f) },   // Red
            { 2, ChartColor.FromRgba(255, 152, 0, 0.8f) },   // Orange
            { 3, ChartColor.FromRgba(156, 39, 176, 0.8f) },  // Purple
            { 4, ChartColor.FromRgba(255, 235, 59, 0.8f) },  // Yellow
            { 5, ChartColor.FromRgba(233, 30, 99, 0.8f) }    // Pink
        };

        foreach (var kvp in ratingDistribution.OrderBy(x => x.Key))
        {
            var labelKey = kvp.Key == 0 ? "Unknown" : $"{kvp.Key} Star";
            labels.Add(L[labelKey]);
            data.Add(kvp.Value);
            backgroundColors.Add(colors.GetValueOrDefault(kvp.Key, ChartColor.FromRgba(128, 128, 128, 0.8f)));
        }

        var dataset = new PieChartDataset<int>
        {
            Label = L["RatingCount"],
            Data = data,
            BackgroundColor = backgroundColors
        };

        await pieChart.AddLabelsDatasetsAndUpdate(labels.ToArray(), dataset);
    }

    protected virtual async Task UpdateBarChartAsync(Dictionary<string, double> criteriaRatings)
    {
        await barChart.Clear();

        var labels = criteriaRatings.Keys.ToArray();
        var data = criteriaRatings.Values.ToList();

        var dataset = new BarChartDataset<double>
        {
            Label = L["AverageRating"],
            Data = data,
            BackgroundColor = new List<string> 
            { 
                ChartColor.FromRgba(54, 162, 235, 0.6f) 
            },
            BorderColor = new List<string> 
            { 
                ChartColor.FromRgba(54, 162, 235, 1f) 
            },
            BorderWidth = 1
        };

        var options = new BarChartOptions
        {
            Scales = new ChartScales
            {
                Y = new ChartAxis
                {
                    Min = 0,
                    Max = 5,
                    Ticks = new ChartAxisTicks
                    {
                        StepSize = 0.5
                    }
                }
            },
            Plugins = new ChartPlugins
            {
                Legend = new ChartLegend
                {
                    Display = false
                }
            }
        };

        await barChart.SetOptions(options);
        await barChart.AddLabelsDatasetsAndUpdate(labels, dataset);
        await barChart.Update();
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
