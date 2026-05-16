using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using HC.Reports;

namespace HC.Controllers.Reports;

[RemoteService]
[Area("app")]
[ControllerName("Report")]
[Route("api/app/reports")]
public class ReportController : ReportControllerBase, IReportsAppService
{
    public ReportController(IReportsAppService reportsAppService) : base(reportsAppService)
    {
    }

    [HttpGet]
    [Route("for-navigation")]
    [AllowAnonymous]
    public virtual Task<PagedResultDto<ReportDto>> GetListForNavigationAsync(GetReportsInput input)
    {
        return _reportsAppService.GetListForNavigationAsync(input);
    }
}