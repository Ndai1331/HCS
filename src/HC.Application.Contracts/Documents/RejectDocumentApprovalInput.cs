using System;
using System.ComponentModel.DataAnnotations;

namespace HC.Documents;

public class RejectDocumentApprovalInput
{
    [Required]
    public Guid DocumentId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;
}
