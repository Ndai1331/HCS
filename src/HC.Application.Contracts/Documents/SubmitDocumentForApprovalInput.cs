using System;
using System.ComponentModel.DataAnnotations;

namespace HC.Documents;

public class SubmitDocumentForApprovalInput
{
    [Required]
    public Guid DocumentId { get; set; }

    [Required]
    public Guid LeaderUserId { get; set; }

    [MaxLength(2000)]
    public string? Message { get; set; }
}
