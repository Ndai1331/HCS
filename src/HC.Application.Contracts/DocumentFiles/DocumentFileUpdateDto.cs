using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;

namespace HC.DocumentFiles;

public abstract class DocumentFileUpdateDtoBase : IHasConcurrencyStamp
{
    [Required]
    public string Name { get; set; } = null!;
    public string? Path { get; set; }

    public string? Hash { get; set; }

    public bool IsSigned { get; set; }

    public DateTime UploadedAt { get; set; }

    public Guid? DocumentId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}