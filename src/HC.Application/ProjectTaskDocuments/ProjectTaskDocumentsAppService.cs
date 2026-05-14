using HC.Shared;
using HC.Documents;
using HC.ProjectTasks;
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
using HC.ProjectTaskDocuments;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace HC.ProjectTaskDocuments;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.ProjectTaskDocuments.Default)]
public abstract class ProjectTaskDocumentsAppServiceBase : HCAppService
{
    protected IDistributedCache<ProjectTaskDocumentDownloadTokenCacheItem, string> _downloadTokenCache;
    protected IProjectTaskDocumentRepository _projectTaskDocumentRepository;
    protected ProjectTaskDocumentManager _projectTaskDocumentManager;
    protected IRepository<HC.ProjectTasks.ProjectTask, Guid> _projectTaskRepository;
    protected IRepository<HC.Documents.Document, Guid> _documentRepository;

    public ProjectTaskDocumentsAppServiceBase(IProjectTaskDocumentRepository projectTaskDocumentRepository, ProjectTaskDocumentManager projectTaskDocumentManager, IDistributedCache<ProjectTaskDocumentDownloadTokenCacheItem, string> downloadTokenCache, IRepository<HC.ProjectTasks.ProjectTask, Guid> projectTaskRepository, IRepository<HC.Documents.Document, Guid> documentRepository)
    {
        _downloadTokenCache = downloadTokenCache;
        _projectTaskDocumentRepository = projectTaskDocumentRepository;
        _projectTaskDocumentManager = projectTaskDocumentManager;
        _projectTaskRepository = projectTaskRepository;
        _documentRepository = documentRepository;
    }

    public virtual async Task<PagedResultDto<ProjectTaskDocumentWithNavigationPropertiesDto>> GetListAsync(GetProjectTaskDocumentsInput input)
    {
        var totalCount = await _projectTaskDocumentRepository.GetCountAsync(input.FilterText, input.DocumentPurpose, input.ProjectTaskId, input.DocumentId);
        var items = await _projectTaskDocumentRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.DocumentPurpose, input.ProjectTaskId, input.DocumentId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<ProjectTaskDocumentWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<ProjectTaskDocumentWithNavigationProperties>, List<ProjectTaskDocumentWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<ProjectTaskDocumentWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<ProjectTaskDocumentWithNavigationProperties, ProjectTaskDocumentWithNavigationPropertiesDto>(await _projectTaskDocumentRepository.GetWithNavigationPropertiesAsync(id));
    }

    public virtual async Task<ProjectTaskDocumentDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<ProjectTaskDocument, ProjectTaskDocumentDto>(await _projectTaskDocumentRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetProjectTaskLookupAsync(LookupRequestDto input)
    {
        var query = (await _projectTaskRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => (x.Code != null && x.Code.Contains(input.Filter)) || (x.Title != null && x.Title.Contains(input.Filter)));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.ProjectTasks.ProjectTask>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.ProjectTasks.ProjectTask>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetDocumentLookupAsync(LookupRequestDto input)
    {
        var query = (await _documentRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), 
        x => x.Title != null && x.Title.Contains(input.Filter) 
            || x.StorageNumber != null && x.StorageNumber.Contains(input.Filter)
            || x.No != null && x.No.Contains(input.Filter)
        );
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.Documents.Document>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.Documents.Document>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(HCPermissions.ProjectTaskDocuments.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _projectTaskDocumentRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.ProjectTaskDocuments.Create)]
    public virtual async Task<ProjectTaskDocumentDto> CreateAsync(ProjectTaskDocumentCreateDto input)
    {
        if (input.ProjectTaskId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["ProjectTask"]]);
        }

        if (input.DocumentId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Document"]]);
        }

        var projectTaskDocument = await _projectTaskDocumentManager.CreateAsync(input.ProjectTaskId, input.DocumentId, input.DocumentPurpose);
        return ObjectMapper.Map<ProjectTaskDocument, ProjectTaskDocumentDto>(projectTaskDocument);
    }

    [Authorize(HCPermissions.ProjectTaskDocuments.Edit)]
    public virtual async Task<ProjectTaskDocumentDto> UpdateAsync(Guid id, ProjectTaskDocumentUpdateDto input)
    {
        if (input.ProjectTaskId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["ProjectTask"]]);
        }

        if (input.DocumentId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Document"]]);
        }

        var projectTaskDocument = await _projectTaskDocumentManager.UpdateAsync(id, input.ProjectTaskId, input.DocumentId, input.DocumentPurpose, input.ConcurrencyStamp);
        return ObjectMapper.Map<ProjectTaskDocument, ProjectTaskDocumentDto>(projectTaskDocument);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(ProjectTaskDocumentExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var projectTaskDocuments = await _projectTaskDocumentRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.DocumentPurpose, input.ProjectTaskId, input.DocumentId);
        var items = projectTaskDocuments.Select(item => new { DocumentPurpose = item.ProjectTaskDocument.DocumentPurpose, ProjectTask = item.ProjectTask?.Title, Document = item.Document?.Title, });
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(items);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "ProjectTaskDocuments.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.ProjectTaskDocuments.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> projecttaskdocumentIds)
    {
        await _projectTaskDocumentRepository.DeleteManyAsync(projecttaskdocumentIds);
    }

    [Authorize(HCPermissions.ProjectTaskDocuments.Delete)]
    public virtual async Task DeleteAllAsync(GetProjectTaskDocumentsInput input)
    {
        await _projectTaskDocumentRepository.DeleteAllAsync(input.FilterText, input.DocumentPurpose, input.ProjectTaskId, input.DocumentId);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new ProjectTaskDocumentDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}