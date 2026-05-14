using HC.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace HC.CalendarEventParticipants;

public partial interface ICalendarEventParticipantsAppService : IApplicationService
{
    Task<PagedResultDto<CalendarEventParticipantWithNavigationPropertiesDto>> GetListAsync(GetCalendarEventParticipantsInput input);
    Task<CalendarEventParticipantWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<CalendarEventParticipantDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetCalendarEventLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<CalendarEventParticipantDto> CreateAsync(CalendarEventParticipantCreateDto input);
    Task<CalendarEventParticipantDto> UpdateAsync(Guid id, CalendarEventParticipantUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(CalendarEventParticipantExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> calendareventparticipantIds);
    Task DeleteAllAsync(GetCalendarEventParticipantsInput input);
    Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();
}