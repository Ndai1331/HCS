using System;

namespace HC.Documents;

public abstract class DocumentExcelDtoBase
{
    public string? No { get; set; }

    public string Title { get; set; } = null!;
    public string? CurrentStatus { get; set; }

    public DateTime CompletedTime { get; set; }

    public string StorageNumber { get; set; } = null!;
    public DateTime IncommingDate { get; set; }
}