using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace HC.SignatureSettings;

public abstract class SignatureSettingDtoBase : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public string ProviderCode { get; set; } = null!;
    public string ProviderType { get; set; } = null!;
    public string ApiEndpoint { get; set; } = null!;
    public string? LayoutImg { get; set; }
    public int ApiTimeout { get; set; }

    public string DefaultSignType { get; set; } = null!;
    public bool AllowElectronicSign { get; set; }

    public bool AllowDigitalSign { get; set; }

    public bool RequireOtp { get; set; }

    public int SignWidth { get; set; }

    public int SignHeight { get; set; }

    public string SignedFileSuffix { get; set; } = null!;
    public bool KeepOriginalFile { get; set; }

    public bool OverwriteSignedFile { get; set; }

    public bool EnableSignLog { get; set; }

    public bool IsActive { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}