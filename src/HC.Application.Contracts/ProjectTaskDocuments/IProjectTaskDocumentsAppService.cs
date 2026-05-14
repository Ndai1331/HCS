using HC.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace HC.ProjectTaskDocuments;

public partial interface IProjectTaskDocumentsAppService : IApplicationService
{
    Task<PagedResultDto<ProjectTaskDocumentWithNavigationPropertiesDto>> GetListAsync(GetProjectTaskDocumentsInput input);
    Task<ProjectTaskDocumentWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<ProjectTaskDocumentDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetProjectTaskLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetDocumentLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<ProjectTaskDocumentDto> CreateAsync(ProjectTaskDocumentCreateDto input);
    Task<ProjectTaskDocumentDto> UpdateAsync(Guid id, ProjectTaskDocumentUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(ProjectTaskDocumentExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> projecttaskdocumentIds);
    Task DeleteAllAsync(GetProjectTaskDocumentsInput input);
    Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();
}