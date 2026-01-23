using System;

namespace HC.Chat.Messages;

public class MessageFileDto
{
    public Guid Id { get; set; }
    public Guid? MessageId { get; set; } // Nullable for pre-upload pattern
    public string FileName { get; set; }
    public string ContentType { get; set; }
    public long FileSize { get; set; }
    public string FileExtension { get; set; }
    public string FilePath { get; set; }
    public string DownloadUrl { get; set; } // Generated download URL
    public DateTime CreationTime { get; set; }
}
