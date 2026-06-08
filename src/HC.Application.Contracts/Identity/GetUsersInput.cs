using System;
using Volo.Abp.Application.Dtos;

namespace HC.Identity;

public class GetUsersInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    public Guid? RoleId { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    public string? UserName { get; set; }

    public string? PhoneNumber { get; set; }

    public string? FullName { get; set; }

    public bool? IsActive { get; set; }
}
