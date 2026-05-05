using System;

namespace HC.Reports;

public abstract class ReportExcelDtoBase
{
    public string Name { get; set; } = null!;
    public string Url { get; set; } = null!;
    public int SortOrder { get; set; }

    public string? Image { get; set; }
}