using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;

namespace HC.Documents;

public abstract class DocumentUpdateDtoBase : IHasConcurrencyStamp
{
    [StringLength(DocumentConsts.NoMaxLength)]
    public string? No { get; set; }

    [Required]
    public string Title { get; set; } = null!;
    [StringLength(DocumentConsts.CurrentStatusMaxLength)]
    public string? CurrentStatus { get; set; }

    public DateTime CompletedTime { get; set; }

    [Required]
    [StringLength(DocumentConsts.StorageNumberMaxLength)]
    public string StorageNumber { get; set; } = null!;
    public DateTime IncommingDate { get; set; }

    public Guid? FieldId { get; set; }

    public Guid? UnitId { get; set; }

    public Guid? WorkflowId { get; set; }

    public Guid? StatusId { get; set; }

    public Guid TypeId { get; set; }

    public Guid UrgencyLevelId { get; set; }

    public Guid SecrecyLevelId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}