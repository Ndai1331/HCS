using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HC.Reports;

public abstract class ReportCreateDtoBase
{
    [Required]
    [StringLength(ReportConsts.NameMaxLength)]
    public string Name { get; set; } = null!;
    [Required]
    [StringLength(ReportConsts.UrlMaxLength)]
    public string Url { get; set; } = null!;
    public int SortOrder { get; set; } = 1;
    [StringLength(ReportConsts.ImageMaxLength)]
    public string? Image { get; set; }
}