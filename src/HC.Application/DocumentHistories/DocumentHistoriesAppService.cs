using HC.Shared;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using HC.Permissions;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace HC.DocumentHistories;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.DocumentHistories.Default)]
public abstract class DocumentHistoriesAppServiceBase : HCAppService
{
    protected ILogger<DocumentHistoriesAppServiceBase> Logger;
    protected IDistributedCache<DocumentHistoryDownloadTokenCacheItem, string> _downloadTokenCache;
    protected IDocumentHistoryRepository _documentHistoryRepository;
    protected DocumentHistoryManager _documentHistoryManager;
    protected IRepository<HC.Documents.Document, Guid> _documentRepository;
    protected IRepository<Volo.Abp.Identity.IdentityUser, Guid> _identityUserRepository;

    public DocumentHistoriesAppServiceBase(IDocumentHistoryRepository documentHistoryRepository, DocumentHistoryManager documentHistoryManager, IDistributedCache<DocumentHistoryDownloadTokenCacheItem, string> downloadTokenCache, IRepository<HC.Documents.Document, Guid> documentRepository, IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository, ILogger<DocumentHistoriesAppServiceBase> logger)
    {
        _downloadTokenCache = downloadTokenCache;
        _documentHistoryRepository = documentHistoryRepository;
        _documentHistoryManager = documentHistoryManager;
        _documentRepository = documentRepository;
        _identityUserRepository = identityUserRepository;
        Logger = logger;
    }

    public virtual async Task<PagedResultDto<DocumentHistoryWithNavigationPropertiesDto>> GetListAsync(GetDocumentHistoriesInput input)
    {
        var totalCount = await _documentHistoryRepository.GetCountAsync(input.FilterText, input.Comment, input.Action, input.DocumentId, input.FromUser, input.ToUser);
        var items = await _documentHistoryRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.Comment, input.Action, input.DocumentId, input.FromUser, input.ToUser, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<DocumentHistoryWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<DocumentHistoryWithNavigationProperties>, List<DocumentHistoryWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<DocumentHistoryWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<DocumentHistoryWithNavigationProperties, DocumentHistoryWithNavigationPropertiesDto>(await _documentHistoryRepository.GetWithNavigationPropertiesAsync(id));
    }

    public virtual async Task<DocumentHistoryDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<DocumentHistory, DocumentHistoryDto>(await _documentHistoryRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetDocumentLookupAsync(LookupRequestDto input)
    {
        var query = (await _documentRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Title != null && x.Title.Contains(input.Filter));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.Documents.Document>();
        var totalCount = await AsyncExecuter.CountAsync(query);
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.Documents.Document>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        var query = (await _identityUserRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<Volo.Abp.Identity.IdentityUser>();
        var totalCount = await AsyncExecuter.CountAsync(query);
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<Volo.Abp.Identity.IdentityUser>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(HCPermissions.DocumentHistories.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _documentHistoryRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.DocumentHistories.Create)]
    public virtual async Task<DocumentHistoryDto> CreateAsync(DocumentHistoryCreateDto input)
    {
        if (input.DocumentId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Document"]]);
        }

        if (input.ToUser == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        var documentHistory = await _documentHistoryManager.CreateAsync(input.DocumentId, input.FromUser, input.ToUser, input.Action, input.Comment);
        return ObjectMapper.Map<DocumentHistory, DocumentHistoryDto>(documentHistory);
    }

    [Authorize(HCPermissions.DocumentHistories.Edit)]
    public virtual async Task<DocumentHistoryDto> UpdateAsync(Guid id, DocumentHistoryUpdateDto input)
    {
        if (input.DocumentId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Document"]]);
        }

        if (input.ToUser == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        var documentHistory = await _documentHistoryManager.UpdateAsync(id, input.DocumentId, input.FromUser, input.ToUser, input.Action, input.Comment, input.ConcurrencyStamp);
        return ObjectMapper.Map<DocumentHistory, DocumentHistoryDto>(documentHistory);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(DocumentHistoryExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var documentHistories = await _documentHistoryRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.Comment, input.Action, input.DocumentId, input.FromUser, input.ToUser);
        var items = documentHistories.Select(item => new { Comment = item.DocumentHistory.Comment, Action = item.DocumentHistory.Action, Document = item.Document?.Title, FromUser = item.FromUser?.Name, ToUser = item.ToUser?.Name, });
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(items);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "DocumentHistories.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.DocumentHistories.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> documenthistoryIds)
    {
        await _documentHistoryRepository.DeleteManyAsync(documenthistoryIds);
    }

    [Authorize(HCPermissions.DocumentHistories.Delete)]
    public virtual async Task DeleteAllAsync(GetDocumentHistoriesInput input)
    {
        await _documentHistoryRepository.DeleteAllAsync(input.FilterText, input.Comment, input.Action, input.DocumentId, input.FromUser, input.ToUser);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new DocumentHistoryDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }

    public async Task<PagedResultDto<DocumentHistoryWithNavigationPropertiesDto>> GetHistoryByDocumentIdAsync(
        GetDocumentHistoriesInput input)
    {
        if (input.DocumentId == null)
        {
            throw new UserFriendlyException("DocumentId is required");
        }
        var histories = await _documentHistoryRepository.GetHistoryByDocumentIdAsync(
            input.DocumentId.Value,
            input.SkipCount,
            input.MaxResultCount);

        var totalCount = await _documentHistoryRepository.GetCountByDocumentIdAsync(input.DocumentId.Value);

        return new PagedResultDto<DocumentHistoryWithNavigationPropertiesDto>
        {
            Items = ObjectMapper.Map<List<DocumentHistoryWithNavigationProperties>, List<DocumentHistoryWithNavigationPropertiesDto>>(histories),
            TotalCount = totalCount
        };
    }
}

public class DocumentHistoriesAppService : DocumentHistoriesAppServiceBase, IDocumentHistoriesAppService
{
    public DocumentHistoriesAppService(
        IDocumentHistoryRepository documentHistoryRepository,
        DocumentHistoryManager documentHistoryManager,
        IDistributedCache<DocumentHistoryDownloadTokenCacheItem, string> downloadTokenCache,
        IRepository<HC.Documents.Document, Guid> documentRepository,
        IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository,
        ILogger<DocumentHistoriesAppServiceBase> logger)
        : base(documentHistoryRepository, documentHistoryManager, downloadTokenCache, documentRepository, identityUserRepository, logger)
    {
    }
}