using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace HC.Reports;

public abstract class ReportDtoBase : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public string Name { get; set; } = null!;
    public string Url { get; set; } = null!;
    public int SortOrder { get; set; }

    public string? Image { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}