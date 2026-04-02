using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace HC.UserSignatures;

public abstract class UserSignatureDtoBase : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public string SignType { get; set; } = null!;
    public string ProviderCode { get; set; } = null!;
    public string? TokenRef { get; set; }
    public string? SealImg { get; set; }

    public string SignatureImage { get; set; } = null!;
    public DateTime? ValidFrom { get; set; }

    public DateTime? ValidTo { get; set; }

    public bool IsActive { get; set; }

    public Guid IdentityUserId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}