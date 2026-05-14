using HC.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace HC.SurveyFiles;

public partial interface ISurveyFilesAppService : IApplicationService
{
    Task<PagedResultDto<SurveyFileWithNavigationPropertiesDto>> GetListAsync(GetSurveyFilesInput input);
    Task<SurveyFileWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<SurveyFileDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetSurveySessionLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<SurveyFileDto> CreateAsync(SurveyFileCreateDto input);
    Task<SurveyFileDto> UpdateAsync(Guid id, SurveyFileUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(SurveyFileExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> surveyfileIds);
    Task DeleteAllAsync(GetSurveyFilesInput input);
    Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();
}