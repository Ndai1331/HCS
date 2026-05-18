using HC.Shared;
using HC.DocumentFiles;
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
using HC.DocumentWorkflowInstanceFiles;
using Microsoft.EntityFrameworkCore;

namespace HC.DocumentWorkflowInstanceFiles;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.DocumentWorkflowInstanceFiles.Default)]
public abstract class DocumentWorkflowInstanceFilesAppServiceBase : HCAppService
{
    protected IDocumentWorkflowInstanceFileRepository _documentWorkflowInstanceFileRepository;
    protected DocumentWorkflowInstanceFileManager _documentWorkflowInstanceFileManager;
    protected IRepository<HC.DocumentFiles.DocumentFile, Guid> _documentFileRepository;

    public DocumentWorkflowInstanceFilesAppServiceBase(IDocumentWorkflowInstanceFileRepository documentWorkflowInstanceFileRepository, DocumentWorkflowInstanceFileManager documentWorkflowInstanceFileManager, IRepository<HC.DocumentFiles.DocumentFile, Guid> documentFileRepository)
    {
        _documentWorkflowInstanceFileRepository = documentWorkflowInstanceFileRepository;
        _documentWorkflowInstanceFileManager = documentWorkflowInstanceFileManager;
        _documentFileRepository = documentFileRepository;
    }

    public virtual async Task<PagedResultDto<DocumentWorkflowInstanceFileDto>> GetListByDocumentWorkflowInstanceIdAsync(GetDocumentWorkflowInstanceFileListInput input)
    {
        var documentWorkflowInstanceFiles = await _documentWorkflowInstanceFileRepository.GetListByDocumentWorkflowInstanceIdAsync(input.DocumentWorkflowInstanceId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<DocumentWorkflowInstanceFileDto>
        {
            TotalCount = await _documentWorkflowInstanceFileRepository.GetCountByDocumentWorkflowInstanceIdAsync(input.DocumentWorkflowInstanceId),
            Items = ObjectMapper.Map<List<DocumentWorkflowInstanceFile>, List<DocumentWorkflowInstanceFileDto>>(documentWorkflowInstanceFiles)
        };
    }

    public virtual async Task<PagedResultDto<DocumentWorkflowInstanceFileWithNavigationPropertiesDto>> GetListWithNavigationPropertiesByDocumentWorkflowInstanceIdAsync(GetDocumentWorkflowInstanceFileListInput input)
    {
        var documentWorkflowInstanceFiles = await _documentWorkflowInstanceFileRepository.GetListWithNavigationPropertiesByDocumentWorkflowInstanceIdAsync(input.DocumentWorkflowInstanceId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<DocumentWorkflowInstanceFileWithNavigationPropertiesDto>
        {
            TotalCount = await _documentWorkflowInstanceFileRepository.GetCountByDocumentWorkflowInstanceIdAsync(input.DocumentWorkflowInstanceId),
            Items = ObjectMapper.Map<List<DocumentWorkflowInstanceFileWithNavigationProperties>, List<DocumentWorkflowInstanceFileWithNavigationPropertiesDto>>(documentWorkflowInstanceFiles)
        };
    }

    public virtual async Task<PagedResultDto<DocumentWorkflowInstanceFileWithNavigationPropertiesDto>> GetListAsync(GetDocumentWorkflowInstanceFilesInput input)
    {
        var totalCount = await _documentWorkflowInstanceFileRepository.GetCountAsync(input.FilterText, input.DocumentFileId);
        var items = await _documentWorkflowInstanceFileRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.DocumentFileId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<DocumentWorkflowInstanceFileWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<DocumentWorkflowInstanceFileWithNavigationProperties>, List<DocumentWorkflowInstanceFileWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<DocumentWorkflowInstanceFileWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<DocumentWorkflowInstanceFileWithNavigationProperties, DocumentWorkflowInstanceFileWithNavigationPropertiesDto>(await _documentWorkflowInstanceFileRepository.GetWithNavigationPropertiesAsync(id));
    }

    public virtual async Task<DocumentWorkflowInstanceFileDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<DocumentWorkflowInstanceFile, DocumentWorkflowInstanceFileDto>(await _documentWorkflowInstanceFileRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetDocumentFileLookupAsync(LookupRequestDto input)
    {
        var query = (await _documentFileRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.DocumentFiles.DocumentFile>();
        var totalCount = await query.CountAsync();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.DocumentFiles.DocumentFile>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(HCPermissions.DocumentWorkflowInstanceFiles.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _documentWorkflowInstanceFileRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.DocumentWorkflowInstanceFiles.Create)]
    public virtual async Task<DocumentWorkflowInstanceFileDto> CreateAsync(DocumentWorkflowInstanceFileCreateDto input)
    {
        if (input.DocumentFileId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["DocumentFile"]]);
        }

        var documentWorkflowInstanceFile = await _documentWorkflowInstanceFileManager.CreateAsync(input.DocumentWorkflowInstanceId, input.DocumentFileId);
        return ObjectMapper.Map<DocumentWorkflowInstanceFile, DocumentWorkflowInstanceFileDto>(documentWorkflowInstanceFile);
    }

    [Authorize(HCPermissions.DocumentWorkflowInstanceFiles.Edit)]
    public virtual async Task<DocumentWorkflowInstanceFileDto> UpdateAsync(Guid id, DocumentWorkflowInstanceFileUpdateDto input)
    {
        if (input.DocumentFileId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["DocumentFile"]]);
        }

        var documentWorkflowInstanceFile = await _documentWorkflowInstanceFileManager.UpdateAsync(id, input.DocumentWorkflowInstanceId, input.DocumentFileId);
        return ObjectMapper.Map<DocumentWorkflowInstanceFile, DocumentWorkflowInstanceFileDto>(documentWorkflowInstanceFile);
    }
}