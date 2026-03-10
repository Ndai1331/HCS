using Volo.Abp.Application.Dtos;
using System;

namespace HC.SurveyResults;

public class GetSurveyResultSessionSummariesInput : PagedAndSortedResultRequestDto
{
    public Guid? SurveyLocationId { get; set; }
}
