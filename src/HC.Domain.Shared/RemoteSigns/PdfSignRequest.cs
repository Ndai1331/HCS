#nullable disable
namespace HC.RemoteSigns;

/// <summary>
/// Payload for TAG / REMOTE_CA PDF sign API (/api/v2/pdf/sign/originaldata).
/// Property names match TAG server expectations (aligned with legacy mework integration).
/// </summary>
public class PdfSignRequest
{
    public string base64image { get; set; }

    public string base64pdf { get; set; } = "";

    public string hashalg { get; set; } = "SHA256";

    public int height { get; set; } = 80;

    public int pagesign { get; set; } = 1;

    public string signaturename { get; set; } = "";

    public string textout { get; set; } = "";

    public int typesignature { get; set; } = 3;

    public int width { get; set; }

    public int xpoint { get; set; }

    public int ypoint { get; set; }

    public string textoutcolor { get; set; } = "0,0,0";

    public string TextLocationIdentifier { get; set; }

    public bool? AppendDateSign { get; set; } = true;

    public string DateFormatString { get; set; } = "dd/MM/yyyy HH:mm:ss";

    public float? FontSize { get; set; } = 10f;

    public string base64SignImage { get; set; }

    public string yPointOffset { get; set; } = "center";
}
