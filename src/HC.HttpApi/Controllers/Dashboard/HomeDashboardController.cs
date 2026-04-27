using System.Threading.Tasks;
using Asp.Versioning;
using HC.Dashboard;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace HC.Controllers.Dashboard;

[RemoteService]
[Area("app")]
[ControllerName("HomeDashboard")]
[Route("api/app/home-dashboard")]
public class HomeDashboardController : HCController, IHomeDashboardAppService
{
    private readonly IHomeDashboardAppService _homeDashboardAppService;

    public HomeDashboardController(IHomeDashboardAppService homeDashboardAppService)
    {
        _homeDashboardAppService = homeDashboardAppService;
    }

    /// <summary>
    /// Aggregates home (Index) dashboard data in one round-trip. Scoped to the current user and optional date range.
    /// </summary>
    [HttpGet]
    [Route("dashboard-bundle")]
    public virtual Task<HomeDashboardBundleDto> GetDashboardBundleAsync([FromQuery] GetHomeDashboardBundleInput input)
    {
        return _homeDashboardAppService.GetDashboardBundleAsync(input);
    }
}
