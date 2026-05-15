using HC.Shared;
using HC.Documents;
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
using HC.DocumentFiles;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace HC.DocumentFiles;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.DocumentFiles.Default)]
public abstract class DocumentFilesAppServiceBase : HCAppService
{
    protected IDistributedCache<DocumentFileDownloadTokenCacheItem, string> _downloadTokenCache;
    protected IDocumentFileRepository _documentFileRepository;
    protected DocumentFileManager _documentFileManager;
    protected IRepository<HC.Documents.Document, Guid> _documentRepository;

    public DocumentFilesAppServiceBase(IDocumentFileRepository documentFileRepository, DocumentFileManager documentFileManager, IDistributedCache<DocumentFileDownloadTokenCacheItem, string> downloadTokenCache, IRepository<HC.Documents.Document, Guid> documentRepository)
    {
        _downloadTokenCache = downloadTokenCache;
        _documentFileRepository = documentFileRepository;
        _documentFileManager = documentFileManager;
        _documentRepository = documentRepository;
    }

    public virtual async Task<PagedResultDto<DocumentFileWithNavigationPropertiesDto>> GetListAsync(GetDocumentFilesInput input)
    {
        var totalCount = await _documentFileRepository.GetCountAsync(input.FilterText, input.Name, input.Path, input.Hash, input.IsSigned, input.UploadedAtMin, input.UploadedAtMax, input.DocumentId);
        var items = await _documentFileRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.Name, input.Path, input.Hash, input.IsSigned, input.UploadedAtMin, input.UploadedAtMax, input.DocumentId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<DocumentFileWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<DocumentFileWithNavigationProperties>, List<DocumentFileWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<DocumentFileWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<DocumentFileWithNavigationProperties, DocumentFileWithNavigationPropertiesDto>(await _documentFileRepository.GetWithNavigationPropertiesAsync(id));
    }

    public virtual async Task<DocumentFileDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<DocumentFile, DocumentFileDto>(await _documentFileRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetDocumentLookupAsync(LookupRequestDto input)
    {
        var query = (await _documentRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Title != null && x.Title.Contains(input.Filter));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.Documents.Document>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.Documents.Document>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(HCPermissions.DocumentFiles.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _documentFileRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.DocumentFiles.Create)]
    public virtual async Task<DocumentFileDto> CreateAsync(DocumentFileCreateDto input)
    {
        var documentFile = await _documentFileManager.CreateAsync(input.DocumentId, input.Name, input.IsSigned, input.UploadedAt, input.Path, input.Hash);
        return ObjectMapper.Map<DocumentFile, DocumentFileDto>(documentFile);
    }

    [Authorize(HCPermissions.DocumentFiles.Edit)]
    public virtual async Task<DocumentFileDto> UpdateAsync(Guid id, DocumentFileUpdateDto input)
    {
        var documentFile = await _documentFileManager.UpdateAsync(id, input.DocumentId, input.Name, input.IsSigned, input.UploadedAt, input.Path, input.Hash, input.ConcurrencyStamp);
        return ObjectMapper.Map<DocumentFile, DocumentFileDto>(documentFile);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(DocumentFileExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var documentFiles = await _documentFileRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.Name, input.Path, input.Hash, input.IsSigned, input.UploadedAtMin, input.UploadedAtMax, input.DocumentId);
        var items = documentFiles.Select(item => new { Name = item.DocumentFile.Name, Path = item.DocumentFile.Path, Hash = item.DocumentFile.Hash, IsSigned = item.DocumentFile.IsSigned, UploadedAt = item.DocumentFile.UploadedAt, Document = item.Document?.Title, });
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(items);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "DocumentFiles.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.DocumentFiles.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> documentfileIds)
    {
        await _documentFileRepository.DeleteManyAsync(documentfileIds);
    }

    [Authorize(HCPermissions.DocumentFiles.Delete)]
    public virtual async Task DeleteAllAsync(GetDocumentFilesInput input)
    {
        await _documentFileRepository.DeleteAllAsync(input.FilterText, input.Name, input.Path, input.Hash, input.IsSigned, input.UploadedAtMin, input.UploadedAtMax, input.DocumentId);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new DocumentFileDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}