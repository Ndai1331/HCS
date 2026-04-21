using System;
using HC.DocumentFiles;
using HC.DocumentHistories;
using Volo.Abp.Application.Dtos;

namespace HC.Documents;

/// <summary>
/// Aggregate payload for the DocumentDetail page so a single round-trip
/// replaces the previous (document → files → histories) sequence.
/// </summary>
public class DocumentDetailBundleDto
{
    public DocumentWithNavigationPropertiesDto Document { get; set; } = default!;
    public PagedResultDto<DocumentFileWithNavigationPropertiesDto> Files { get; set; } = default!;
    public PagedResultDto<DocumentHistoryWithNavigationPropertiesDto> Histories { get; set; } = default!;
}

/// <summary>
/// Input options for <see cref="IDocumentsAppService.GetDetailBundleAsync"/>.
/// Pagination defaults match the current Blazor page so no caller change is required.
/// </summary>
public class GetDocumentDetailBundleInput
{
    public Guid DocumentId { get; set; }

    /// <summary>Max files to return (same default as the Blazor list: 200).</summary>
    public int FilesMaxResultCount { get; set; } = 200;

    public int FilesSkipCount { get; set; } = 0;

    /// <summary>Default matches the first history page on the Blazor page.</summary>
    public int HistoriesMaxResultCount { get; set; } = 10;

    public int HistoriesSkipCount { get; set; } = 0;
}
