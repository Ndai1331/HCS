using System;

namespace HC.Documents;

public class OrganizationUnitTreeNodeDto
{
    public Guid Id { get; set; }

    public Guid? ParentId { get; set; }

    public string? Code { get; set; }

    public string DisplayName { get; set; } = string.Empty;
}
