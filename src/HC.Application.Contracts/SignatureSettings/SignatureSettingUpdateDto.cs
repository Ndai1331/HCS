using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;

namespace HC.SignatureSettings;

public abstract class SignatureSettingUpdateDtoBase : IHasConcurrencyStamp
{
    [Required]
    public string ProviderCode { get; set; } = null!;
    [Required]
    public string ProviderType { get; set; } = null!;
    [Required]
    public string ApiEndpoint { get; set; } = null!;
    public string? LayoutImg { get; set; }
    public int ApiTimeout { get; set; }

    [Required]
    public string DefaultSignType { get; set; } = null!;
    public bool AllowElectronicSign { get; set; }

    public bool AllowDigitalSign { get; set; }

    public bool RequireOtp { get; set; }

    public int SignWidth { get; set; }

    public int SignHeight { get; set; }

    [Required]
    public string SignedFileSuffix { get; set; } = null!;
    public bool KeepOriginalFile { get; set; }

    public bool OverwriteSignedFile { get; set; }

    public bool EnableSignLog { get; set; }

    public bool IsActive { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}