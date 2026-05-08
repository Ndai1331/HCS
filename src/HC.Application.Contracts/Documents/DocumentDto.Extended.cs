using System;

namespace HC.Documents;

public class DocumentDto : DocumentDtoBase
{
    /// <summary>
    /// User who sent the document (Send flow).
    /// </summary>
    public Guid? FromUserId { get; set; }

    /// <summary>
    /// Denormalized individual recipient when send targets one user.
    /// </summary>
    public Guid? ReceiverUserId { get; set; }

    /// <summary>
    /// Denormalized department recipient when send targets one department.
    /// </summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>
    /// ABP Identity organization unit recipient when send targets one department.
    /// </summary>
    public Guid? OrganizationUnitId { get; set; }

    /// <summary>
    /// Workflow duplicate → original document id (manage-documents row).
    /// </summary>
    public Guid? ParentDocumentId { get; set; }
}