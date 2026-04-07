using System;
using System.ComponentModel.DataAnnotations;

namespace HC.Documents;

public class ApproveDocumentWithNoteInput
{
    [Required]
    public Guid DocumentId { get; set; }

    [Range(1, int.MaxValue)]
    public int PageNumber { get; set; }

    public double PdfX { get; set; }

    public double PdfY { get; set; }

    [Required]
    [MaxLength(4000)]
    public string NoteContent { get; set; } = string.Empty;
}
