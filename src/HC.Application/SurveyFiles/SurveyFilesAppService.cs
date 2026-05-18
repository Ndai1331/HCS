using HC.Shared;
using HC.SurveySessions;
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
using HC.SurveyFiles;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.EntityFrameworkCore;

namespace HC.SurveyFiles;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.SurveyFiles.Default)]
public abstract class SurveyFilesAppServiceBase : HCAppService
{
    protected IDistributedCache<SurveyFileDownloadTokenCacheItem, string> _downloadTokenCache;
    protected ISurveyFileRepository _surveyFileRepository;
    protected SurveyFileManager _surveyFileManager;
    protected IRepository<HC.SurveySessions.SurveySession, Guid> _surveySessionRepository;

    public SurveyFilesAppServiceBase(ISurveyFileRepository surveyFileRepository, SurveyFileManager surveyFileManager, IDistributedCache<SurveyFileDownloadTokenCacheItem, string> downloadTokenCache, IRepository<HC.SurveySessions.SurveySession, Guid> surveySessionRepository)
    {
        _downloadTokenCache = downloadTokenCache;
        _surveyFileRepository = surveyFileRepository;
        _surveyFileManager = surveyFileManager;
        _surveySessionRepository = surveySessionRepository;
    }

    public virtual async Task<PagedResultDto<SurveyFileWithNavigationPropertiesDto>> GetListAsync(GetSurveyFilesInput input)
    {
        var uploaderTypeFilter = input.UploaderType?.ToString();
        var totalCount = await _surveyFileRepository.GetCountAsync(input.FilterText, uploaderTypeFilter, input.FileName, input.FilePath, input.FileSizeMin, input.FileSizeMax, input.MimeType, input.FileType, input.SurveySessionId);
        var items = await _surveyFileRepository.GetListWithNavigationPropertiesAsync(input.FilterText, uploaderTypeFilter, input.FileName, input.FilePath, input.FileSizeMin, input.FileSizeMax, input.MimeType, input.FileType, input.SurveySessionId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<SurveyFileWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<SurveyFileWithNavigationProperties>, List<SurveyFileWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<SurveyFileWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<SurveyFileWithNavigationProperties, SurveyFileWithNavigationPropertiesDto>(await _surveyFileRepository.GetWithNavigationPropertiesAsync(id));
    }

    public virtual async Task<SurveyFileDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<SurveyFile, SurveyFileDto>(await _surveyFileRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetSurveySessionLookupAsync(LookupRequestDto input)
    {
        var query = (await _surveySessionRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.SessionDisplay != null && x.SessionDisplay.Contains(input.Filter));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.SurveySessions.SurveySession>();
        var totalCount = await query.CountAsync();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.SurveySessions.SurveySession>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(HCPermissions.SurveyFiles.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _surveyFileRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.SurveyFiles.Create)]
    public virtual async Task<SurveyFileDto> CreateAsync(SurveyFileCreateDto input)
    {
        if (input.SurveySessionId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["SurveySession"]]);
        }

        var uploaderType = input.UploaderType.ToString();
        var surveyFile = await _surveyFileManager.CreateAsync(input.SurveySessionId, uploaderType, input.FileName, input.FilePath, input.FileSize, input.MimeType, input.FileType);
        return ObjectMapper.Map<SurveyFile, SurveyFileDto>(surveyFile);
    }

    [Authorize(HCPermissions.SurveyFiles.Edit)]
    public virtual async Task<SurveyFileDto> UpdateAsync(Guid id, SurveyFileUpdateDto input)
    {
        if (input.SurveySessionId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["SurveySession"]]);
        }

        var uploaderType = input.UploaderType.ToString();
        var surveyFile = await _surveyFileManager.UpdateAsync(id, input.SurveySessionId, uploaderType, input.FileName, input.FilePath, input.FileSize, input.MimeType, input.FileType, input.ConcurrencyStamp);
        return ObjectMapper.Map<SurveyFile, SurveyFileDto>(surveyFile);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(SurveyFileExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var surveyFiles = await _surveyFileRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.UploaderType, input.FileName, input.FilePath, input.FileSizeMin, input.FileSizeMax, input.MimeType, input.FileType, input.SurveySessionId);
        var items = surveyFiles.Select(item => new { UploaderType = item.SurveyFile.UploaderType, FileName = item.SurveyFile.FileName, FilePath = item.SurveyFile.FilePath, FileSize = item.SurveyFile.FileSize, MimeType = item.SurveyFile.MimeType, FileType = item.SurveyFile.FileType, SurveySession = item.SurveySession?.SessionDisplay, });
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(items);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "SurveyFiles.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.SurveyFiles.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> surveyfileIds)
    {
        await _surveyFileRepository.DeleteManyAsync(surveyfileIds);
    }

    [Authorize(HCPermissions.SurveyFiles.Delete)]
    public virtual async Task DeleteAllAsync(GetSurveyFilesInput input)
    {
        var uploaderTypeFilter = input.UploaderType?.ToString();
        await _surveyFileRepository.DeleteAllAsync(input.FilterText, uploaderTypeFilter, input.FileName, input.FilePath, input.FileSizeMin, input.FileSizeMax, input.MimeType, input.FileType, input.SurveySessionId);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new SurveyFileDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}