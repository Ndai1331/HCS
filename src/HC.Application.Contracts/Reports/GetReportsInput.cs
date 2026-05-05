using Volo.Abp.Application.Dtos;
using System;

namespace HC.Reports;

public abstract class GetReportsInputBase : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    public string? Name { get; set; }

    public string? Url { get; set; }

    public int? SortOrderMin { get; set; }

    public int? SortOrderMax { get; set; }

    public string? Image { get; set; }

    public GetReportsInputBase()
    {
    }
}