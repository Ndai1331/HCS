using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.Reports;
using Volo.Abp.Content;
using HC.Shared;

namespace HC.Controllers.Reports;

[RemoteService]
[Area("app")]
[ControllerName("Report")]
[Route("api/app/reports")]
public abstract class ReportControllerBase : AbpController
{
    protected IReportsAppService _reportsAppService;

    public ReportControllerBase(IReportsAppService reportsAppService)
    {
        _reportsAppService = reportsAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<ReportDto>> GetListAsync(GetReportsInput input)
    {
        return _reportsAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<ReportDto> GetAsync(Guid id)
    {
        return _reportsAppService.GetAsync(id);
    }

    [HttpPost]
    public virtual Task<ReportDto> CreateAsync(ReportCreateDto input)
    {
        return _reportsAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<ReportDto> UpdateAsync(Guid id, ReportUpdateDto input)
    {
        return _reportsAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _reportsAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(ReportExcelDownloadDto input)
    {
        return _reportsAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _reportsAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> reportIds)
    {
        return _reportsAppService.DeleteByIdsAsync(reportIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetReportsInput input)
    {
        return _reportsAppService.DeleteAllAsync(input);
    }
}