using System;

namespace HC.BnnSoftSigns;

/// <summary>
/// Thrown when the PDF does not contain the expected signature placeholder text (e.g. &lt;&lt;Sign01&gt;&gt;).
/// </summary>
public class SignPlaceholderNotFoundException : Exception
{
    public string TextSign { get; }

    public int PageSign { get; }

    public SignPlaceholderNotFoundException(string textSign, int pageSign)
        : base($"Placeholder not found for signing. TextSign={textSign}, PageSign={pageSign}")
    {
        TextSign = textSign;
        PageSign = pageSign;
    }
}
