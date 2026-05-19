using System.ComponentModel.DataAnnotations;

namespace HC.PushNotifications;

public class RegisterPushDeviceTokenDto
{
    [Required]
    [StringLength(PushDeviceTokenConsts.FcmTokenMaxLength)]
    public string Token { get; set; } = null!;

    /// <summary>android | ios | web</summary>
    [Required]
    [StringLength(PushDeviceTokenConsts.PlatformMaxLength)]
    public string Platform { get; set; } = null!;

    [StringLength(PushDeviceTokenConsts.DeviceIdMaxLength)]
    public string? DeviceId { get; set; }
}
