using Volo.Abp.Application.Dtos;
using System;

namespace HC.SurveyResults;

public abstract class SurveyResultExcelDownloadDtoBase
{
    public string DownloadToken { get; set; } = null!;
    public string? FilterText { get; set; }

    public int? RatingMin { get; set; }

    public int? RatingMax { get; set; }

    public Guid? SurveyCriteriaId { get; set; }

    public Guid? SurveySessionId { get; set; }
    public Guid? SurveyLocationId { get; set; }

    public SurveyResultExcelDownloadDtoBase()
    {
    }
}