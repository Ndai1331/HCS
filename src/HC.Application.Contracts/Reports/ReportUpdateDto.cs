using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;

namespace HC.Reports;

public abstract class ReportUpdateDtoBase : IHasConcurrencyStamp
{
    [Required]
    [StringLength(ReportConsts.NameMaxLength)]
    public string Name { get; set; } = null!;
    [Required]
    [StringLength(ReportConsts.UrlMaxLength)]
    public string Url { get; set; } = null!;
    public int SortOrder { get; set; }

    [StringLength(ReportConsts.ImageMaxLength)]
    public string? Image { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}