using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace HC.DocumentFiles;

public abstract class DocumentFileDtoBase : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public string Name { get; set; } = null!;
    public string? Path { get; set; }

    public string? Hash { get; set; }

    public bool IsSigned { get; set; }

    public DateTime UploadedAt { get; set; }

    public Guid? DocumentId { get; set; }

    public Guid? SourceDocxFileId { get; set; }

    public Guid? DerivedPdfFileId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}