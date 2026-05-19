#nullable disable
namespace HC.RemoteSigns;

public class PdfSignResponse
{
    public string description { get; set; }

    public string error { get; set; }

    /// <summary>Base64-encoded signed PDF when successful.</summary>
    public string obj { get; set; }

    public int status { get; set; }
}
