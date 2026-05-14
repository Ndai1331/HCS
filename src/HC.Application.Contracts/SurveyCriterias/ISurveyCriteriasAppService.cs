using HC.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace HC.SurveyCriterias;

public partial interface ISurveyCriteriasAppService : IApplicationService
{
    Task<PagedResultDto<SurveyCriteriaWithNavigationPropertiesDto>> GetListAsync(GetSurveyCriteriasInput input);
    Task<SurveyCriteriaWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<SurveyCriteriaDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetSurveyLocationLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<SurveyCriteriaDto> CreateAsync(SurveyCriteriaCreateDto input);
    Task<SurveyCriteriaDto> UpdateAsync(Guid id, SurveyCriteriaUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(SurveyCriteriaExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> surveycriteriaIds);
    Task DeleteAllAsync(GetSurveyCriteriasInput input);
    Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();
}