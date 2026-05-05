using Asp.Versioning;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
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
}