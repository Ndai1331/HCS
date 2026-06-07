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
using System.Globalization;

namespace HC.Documents;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.Documents.Default)]
public abstract class DocumentsAppServiceBase : HCAppService
{
    protected IDistributedCache<DocumentDownloadTokenCacheItem, string> _downloadTokenCache;
    protected IDocumentRepository _documentRepository;
    protected DocumentManager _documentManager;
    protected IRepository<HC.MasterDatas.MasterData, Guid> _masterDataRepository;
    protected IRepository<HC.Units.Unit, Guid> _unitRepository;
    protected IRepository<HC.Workflows.Workflow, Guid> _workflowRepository;
    protected ILogger<DocumentsAppServiceBase> _logger;
    public DocumentsAppServiceBase(IDocumentRepository documentRepository, DocumentManager documentManager, IDistributedCache<DocumentDownloadTokenCacheItem, string> downloadTokenCache, IRepository<HC.MasterDatas.MasterData, Guid> masterDataRepository, IRepository<HC.Units.Unit, Guid> unitRepository, IRepository<HC.Workflows.Workflow, Guid> workflowRepository, ILogger<DocumentsAppServiceBase> logger)
    {
        _downloadTokenCache = downloadTokenCache;
        _documentRepository = documentRepository;
        _documentManager = documentManager;
        _masterDataRepository = masterDataRepository;
        _unitRepository = unitRepository;
        _workflowRepository = workflowRepository;
        _logger = logger;
    }

    public virtual async Task<PagedResultDto<DocumentWithNavigationPropertiesDto>> GetListAsync(GetDocumentsInput input)
    {
        _logger.LogInformation("GetListAsync start");
        var totalCount = await _documentRepository.GetCountAsync(input.FilterText, input.No, input.Title, input.CurrentStatus, input.CompletedTimeMin, input.CompletedTimeMax, input.StorageNumber, input.IncommingDateMin, input.IncommingDateMax, input.FieldId, input.UnitId, input.WorkflowId, input.StatusId, input.TypeId, input.UrgencyLevelId, input.SecrecyLevelId, input.SourceType, input.CreatorId);
        var items = await _documentRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.No, input.Title, 
        input.CurrentStatus, input.CompletedTimeMin, input.CompletedTimeMax,
         input.StorageNumber, input.IncommingDateMin, input.IncommingDateMax,
          input.FieldId, input.UnitId, input.WorkflowId, input.StatusId, 
          input.TypeId, input.UrgencyLevelId, input.SecrecyLevelId, input.SourceType, input.CreatorId, null,
          input.Sorting,  input.MaxResultCount, input.SkipCount);
        var result = new PagedResultDto<DocumentWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<DocumentWithNavigationProperties>, List<DocumentWithNavigationPropertiesDto>>(items)
        };
        _logger.LogInformation("GetListAsync end");
        return result;
    }

    public virtual async Task<DocumentWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<DocumentWithNavigationProperties, DocumentWithNavigationPropertiesDto>(await _documentRepository.GetWithNavigationPropertiesAsync(id));
    }

    public virtual async Task<DocumentDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<Document, DocumentDto>(await _documentRepository.GetAsync(id));
    }

    // M4: lazily-resolved distributed cache for default (filter-less, first-page) lookup pages.
    // Tenant isolation is handled by ABP's DistributedCache<T> via CurrentTenant.
    protected IDistributedCache<DocumentsLookupCacheItem, string> _documentsLookupCache
        => LazyServiceProvider.LazyGetRequiredService<IDistributedCache<DocumentsLookupCacheItem, string>>();
    protected IDistributedCache<LookupCacheVersionCacheItem, string> _lookupVersionCache
        => LazyServiceProvider.LazyGetRequiredService<IDistributedCache<LookupCacheVersionCacheItem, string>>();

    private static readonly DistributedCacheEntryOptions _lookupCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
    };

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetMasterDataLookupAsync(LookupRequestDto input)
    {
        // Cache key schema: docs:lookup:{scope}:v{version}:tenant:{tenantId|host}:lang:{language}:filter:{filter}:skip:{skip}:take:{take}
        var cacheKey = await BuildLookupCacheKeyAsync("master-data", input);
        var cached = await _documentsLookupCache.GetOrAddAsync(cacheKey,
            async () => await LoadMasterDataLookupPageAsync(input),
            () => _lookupCacheOptions);
        return new PagedResultDto<LookupDto<Guid>> { TotalCount = cached!.TotalCount, Items = cached.Items };
    }

    private async Task<DocumentsLookupCacheItem> LoadMasterDataLookupPageAsync(LookupRequestDto input)
    {
        var baseQuery = (await _masterDataRepository.GetQueryableAsync())
            
            .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter));

        // Project to LookupDto in SQL so we only pull Id/Name and skip ObjectMapper round-trips.
        var lookupQuery = baseQuery
            .OrderBy(x => x.SortOrder)
            .Select(x => new LookupDto<Guid> { Id = x.Id, DisplayName = x.Name });

        var totalCount = await AsyncExecuter.LongCountAsync(baseQuery);
        var items = await AsyncExecuter.ToListAsync(lookupQuery.PageBy(input.SkipCount, input.MaxResultCount));

        return new DocumentsLookupCacheItem { TotalCount = totalCount, Items = items };
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetUnitLookupAsync(LookupRequestDto input)
    {
        // Cache key schema: docs:lookup:{scope}:v{version}:tenant:{tenantId|host}:lang:{language}:filter:{filter}:skip:{skip}:take:{take}
        var cacheKey = await BuildLookupCacheKeyAsync("unit", input);
        var cached = await _documentsLookupCache.GetOrAddAsync(cacheKey,
            async () => await LoadUnitLookupPageAsync(input),
            () => _lookupCacheOptions);
        return new PagedResultDto<LookupDto<Guid>> { TotalCount = cached!.TotalCount, Items = cached.Items };
    }

    private async Task<DocumentsLookupCacheItem> LoadUnitLookupPageAsync(LookupRequestDto input)
    {
        var baseQuery = (await _unitRepository.GetQueryableAsync())
            
            .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter));

        var lookupQuery = baseQuery
            .OrderBy(x => x.Name)
            .Select(x => new LookupDto<Guid> { Id = x.Id, DisplayName = x.Name });

        var totalCount = await AsyncExecuter.LongCountAsync(baseQuery);
        var items = await AsyncExecuter.ToListAsync(lookupQuery.PageBy(input.SkipCount, input.MaxResultCount));

        return new DocumentsLookupCacheItem { TotalCount = totalCount, Items = items };
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetWorkflowLookupAsync(LookupRequestDto input)
    {
        // Cache key schema: docs:lookup:{scope}:v{version}:tenant:{tenantId|host}:lang:{language}:filter:{filter}:skip:{skip}:take:{take}
        var cacheKey = await BuildLookupCacheKeyAsync("workflow", input);
        var cached = await _documentsLookupCache.GetOrAddAsync(cacheKey,
            async () => await LoadWorkflowLookupPageAsync(input),
            () => _lookupCacheOptions);
        return new PagedResultDto<LookupDto<Guid>> { TotalCount = cached!.TotalCount, Items = cached.Items };
    }

    private async Task<DocumentsLookupCacheItem> LoadWorkflowLookupPageAsync(LookupRequestDto input)
    {
        var baseQuery = await _workflowRepository.GetQueryableAsync();
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filterLower = input.Filter.Trim().ToLower();
            baseQuery = baseQuery.Where(x =>
                (x.Name != null && x.Name.ToLower().Contains(filterLower))
                || (x.Code != null && x.Code.ToLower().Contains(filterLower)));
        }

        var lookupQuery = baseQuery
            .OrderBy(x => x.Name)
            .Select(x => new LookupDto<Guid> { Id = x.Id, DisplayName = x.Name });

        var totalCount = await AsyncExecuter.LongCountAsync(baseQuery);
        var items = await AsyncExecuter.ToListAsync(lookupQuery.PageBy(input.SkipCount, input.MaxResultCount));

        return new DocumentsLookupCacheItem { TotalCount = totalCount, Items = items };
    }

    protected async Task<string> BuildLookupCacheKeyAsync(string scope, LookupRequestDto input)
    {
        var tenant = CurrentTenant.Id?.ToString("N") ?? "host";
        var language = CultureInfo.CurrentUICulture.Name?.ToLowerInvariant() ?? "iv";
        var filter = (input.Filter ?? string.Empty).Trim().ToLowerInvariant();
        var version = await GetLookupVersionAsync(scope);
        return $"docs:lookup:{scope}:v{version}:tenant:{tenant}:lang:{language}:filter:{filter}:skip:{input.SkipCount}:take:{input.MaxResultCount}";
    }

    protected async Task<int> GetLookupVersionAsync(string scope)
    {
        var item = await _lookupVersionCache.GetAsync($"lookup-version:{scope}");
        return item?.Version ?? 1;
    }

    [Authorize(HCPermissions.Documents.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _documentRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.Documents.Create)]
    public virtual async Task<DocumentDto> CreateAsync(DocumentCreateDto input)
    {
        if (input.TypeId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["MasterData"]]);
        }

        if (input.UrgencyLevelId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["MasterData"]]);
        }

        if (input.SecrecyLevelId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["MasterData"]]);
        }

        var document = await _documentManager.CreateAsync(input.FieldId, input.UnitId, input.WorkflowId, input.StatusId, input.TypeId, input.UrgencyLevelId, input.SecrecyLevelId, input.Title, input.CompletedTime, input.StorageNumber, input.IncommingDate, input.No, input.CurrentStatus, input.SourceType);
        return ObjectMapper.Map<Document, DocumentDto>(document);
    }

    [Authorize(HCPermissions.Documents.Edit)]
    public virtual async Task<DocumentDto> UpdateAsync(Guid id, DocumentUpdateDto input)
    {
        if (input.TypeId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["MasterData"]]);
        }

        if (input.UrgencyLevelId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["MasterData"]]);
        }

        if (input.SecrecyLevelId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["MasterData"]]);
        }

        var document = await _documentManager.UpdateAsync(id, input.FieldId, input.UnitId, input.WorkflowId, input.StatusId, input.TypeId, input.UrgencyLevelId, input.SecrecyLevelId, input.Title, input.CompletedTime, input.StorageNumber, input.IncommingDate, input.SourceType, input.No, input.CurrentStatus, input.ConcurrencyStamp);
        return ObjectMapper.Map<Document, DocumentDto>(document);
    }

    /// <summary>Excel export via anonymous HTTP GET: requires a one-time token from <see cref="GetDownloadTokenAsync"/> (validated in <see cref="HC.ExcelDownloadAnonymousTokenHelper"/>).</summary>
    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(DocumentExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var documents = await _documentRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.No, input.Title, input.CurrentStatus, input.CompletedTimeMin, input.CompletedTimeMax, input.StorageNumber, input.IncommingDateMin, input.IncommingDateMax, input.FieldId, input.UnitId, input.WorkflowId, input.StatusId, input.TypeId, input.UrgencyLevelId, input.SecrecyLevelId, input.SourceType, input.CreatorId);
        var items = documents.Select(item => new { No = item.Document.No, Title = item.Document.Title, CurrentStatus = item.Document.CurrentStatus, CompletedTime = item.Document.CompletedTime, StorageNumber = item.Document.StorageNumber, IncommingDate = item.Document.IncommingDate, Field = item.Field?.Name, Unit = item.Unit?.Name, Workflow = item.Workflow?.Name, Status = item.Status?.Name, Type = item.Type?.Name, UrgencyLevel = item.UrgencyLevel?.Name, SecrecyLevel = item.SecrecyLevel?.Name, Creator = item.Document.CreatorId });
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(items);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "Documents.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.Documents.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> documentIds)
    {
        await _documentRepository.DeleteManyAsync(documentIds);
    }

    [Authorize(HCPermissions.Documents.Delete)]
    public virtual async Task DeleteAllAsync(GetDocumentsInput input)
    {
        await _documentRepository.DeleteAllAsync(input.FilterText, input.No, input.Title, input.CurrentStatus, input.CompletedTimeMin, input.CompletedTimeMax, input.StorageNumber, input.IncommingDateMin, input.IncommingDateMax, input.FieldId, input.UnitId, input.WorkflowId, input.StatusId, input.TypeId, input.UrgencyLevelId, input.SecrecyLevelId, input.SourceType, input.CreatorId);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new DocumentDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}
