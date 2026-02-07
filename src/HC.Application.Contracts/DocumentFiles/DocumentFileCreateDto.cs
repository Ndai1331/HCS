using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HC.DocumentFiles;

public abstract class DocumentFileCreateDtoBase
{
    [Required]
    public string Name { get; set; } = null!;
    public string? Path { get; set; }

    public string? Hash { get; set; }

    public bool IsSigned { get; set; } = false;
    public DateTime UploadedAt { get; set; }

    public Guid? DocumentId { get; set; }
}