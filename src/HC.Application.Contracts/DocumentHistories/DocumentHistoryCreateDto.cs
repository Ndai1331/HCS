using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HC.DocumentHistories;

public abstract class DocumentHistoryCreateDtoBase
{
    public string? Comment { get; set; }

    [Required]
    [StringLength(DocumentHistoryConsts.ActionMaxLength)]
    public string Action { get; set; } = nameof(DocumentHistoryAction.TRINH);
    public Guid DocumentId { get; set; }

    public Guid? FromUser { get; set; }

    public Guid ToUser { get; set; }
}