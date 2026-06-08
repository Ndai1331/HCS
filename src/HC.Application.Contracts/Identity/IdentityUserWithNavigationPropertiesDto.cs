using System;
using Volo.Abp.Identity;

namespace HC.Identity;

public class IdentityUserWithNavigationPropertiesDto
{
    public IdentityUserDto User { get; set; } = null!;

    public string RolesName { get; set; } = string.Empty;

    public Guid? PositionId { get; set; }

    public string? PositionName { get; set; }

    public string OrganizationUnitsName { get; set; } = string.Empty;
}
