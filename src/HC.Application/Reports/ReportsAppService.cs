using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using HC.Permissions;
using HC.Reports;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;
using HC.Shared;

namespace HC.Reports;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.MasterDatas.ReportsDefault)]
public abstract class ReportsAppServiceBase : HCAppService
{
    protected IDistributedCache<ReportDownloadTokenCacheItem, string> _downloadTokenCache;
    protected IReportRepository _reportRepository;
    protected ReportManager _reportManager;

    public ReportsAppServiceBase(IReportRepository reportRepository, ReportManager reportManager, IDistributedCache<ReportDownloadTokenCacheItem, string> downloadTokenCache)
    {
        _downloadTokenCache = downloadTokenCache;
        _reportRepository = reportRepository;
        _reportManager = reportManager;
    }

    [AllowAnonymous]
    public virtual async Task<PagedResultDto<ReportDto>> GetListAsync(GetReportsInput input)
    {
        var totalCount = await _reportRepository.GetCountAsync(input.FilterText, input.Name, input.Url, input.SortOrderMin, input.SortOrderMax, input.Image);
        var items = await _reportRepository.GetListAsync(input.FilterText, input.Name, input.Url, input.SortOrderMin, input.SortOrderMax, input.Image, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<ReportDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<Report>, List<ReportDto>>(items)
        };
    }

    public virtual async Task<ReportDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<Report, ReportDto>(await _reportRepository.GetAsync(id));
    }

    [Authorize(HCPermissions.MasterDatas.ReportsDelete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _reportRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.MasterDatas.ReportsCreate)]
    public virtual async Task<ReportDto> CreateAsync(ReportCreateDto input)
    {
        var report = await _reportManager.CreateAsync(input.Name, input.Url, input.SortOrder, input.Image);
        return ObjectMapper.Map<Report, ReportDto>(report);
    }

    [Authorize(HCPermissions.MasterDatas.ReportsEdit)]
    public virtual async Task<ReportDto> UpdateAsync(Guid id, ReportUpdateDto input)
    {
        var report = await _reportManager.UpdateAsync(id, input.Name, input.Url, input.SortOrder, input.Image, input.ConcurrencyStamp);
        return ObjectMapper.Map<Report, ReportDto>(report);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(ReportExcelDownloadDto input)
    {
        var downloadToken = await _downloadTokenCache.GetAsync(input.DownloadToken);
        if (downloadToken == null || input.DownloadToken != downloadToken.Token)
        {
            throw new AbpAuthorizationException("Invalid download token: " + input.DownloadToken);
        }

        var items = await _reportRepository.GetListAsync(input.FilterText, input.Name, input.Url, input.SortOrderMin, input.SortOrderMax, input.Image);
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(ObjectMapper.Map<List<Report>, List<ReportExcelDto>>(items));
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "Reports.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.MasterDatas.ReportsDelete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> reportIds)
    {
        await _reportRepository.DeleteManyAsync(reportIds);
    }

    [Authorize(HCPermissions.MasterDatas.ReportsDelete)]
    public virtual async Task DeleteAllAsync(GetReportsInput input)
    {
        await _reportRepository.DeleteAllAsync(input.FilterText, input.Name, input.Url, input.SortOrderMin, input.SortOrderMax, input.Image);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new ReportDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}