using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HtmlToOpenXml;
using SixLabors.ImageSharp;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace HC.DocumentWorkflowInstances;

/// <summary>
/// Replaces placeholders in Word (.docx) documents before converting to PDF.
/// Preserves layout better than replacing in PDF, especially for &lt;&lt;ContentToBeApproved&gt;&gt;.
/// </summary>
public static class WordPlaceholderReplacer
{
    // ~4.2 cm — matches typical trình ký / ký điện tử signature block width in Word templates.
    private const long SignatureImageMaxWidthEmu = 1512000L;
    private const long SignatureImageFallbackHeightEmu = 621000L;
    private const string PreparedBySignPlaceholder = "<<PreparedBySign>>";
    private static readonly char[] NewLineSeparator = ['\n'];

    /// <summary>
    /// Replace placeholders in Word document bytes. Returns modified .docx bytes.
    /// Placeholders: &lt;&lt;DD&gt;&gt;, &lt;&lt;MM&gt;&gt;, &lt;&lt;YYYY&gt;&gt;,
    /// &lt;&lt;ContentToBeApproved&gt;&gt;, &lt;&lt;PreparedBySign&gt;&gt;, &lt;&lt;PreparedFullName&gt;&gt;,
    /// &lt;&lt;PositionName&gt;&gt;, &lt;&lt;ViTriLamViec&gt;&gt;, &lt;&lt;PhongBan&gt;&gt;, &lt;&lt;Department&gt;&gt;
    /// </summary>
    public static byte[] ReplacePlaceholders(
        byte[] docxBytes,
        byte[]? signatureImageBytes,
        string fullName,
        string htmlContent,
        DateTime currentDate,
        string positionText,
        string departmentText)
    {
        var plainContent = HtmlToPlainWithLineBreaks(htmlContent ?? string.Empty);
        var useHtmlForContent = !string.IsNullOrWhiteSpace(htmlContent) && htmlContent.Contains("<") && htmlContent.Contains(">");
        var contentValue = useHtmlForContent ? string.Empty : plainContent;

        var replacements = new (string Placeholder, string Value)[]
        {
            ("<<DD>>", currentDate.ToString("dd")),
            ("<<Day>>", currentDate.ToString("dd")),
            ("<<MM>>", currentDate.ToString("MM")),
            ("<<Month>>", currentDate.ToString("MM")),
            ("<<YYYY>>", currentDate.ToString("yyyy")),
            ("<<Year>>", currentDate.ToString("yyyy")),
            ("<<ContentToBeApproved>>", contentValue),
            ("<<PreparedFullName>>", fullName),
            ("<<PositionName>>", positionText),
            ("<<ViTriLamViec>>", positionText),
            ("<<PhongBan>>", departmentText),
            ("<<Department>>", departmentText),
        };

        // Copy to new stream for editing (Open modifies in place)
        using var stream = new MemoryStream(docxBytes.Length);
        stream.Write(docxBytes, 0, docxBytes.Length);
        stream.Position = 0;

        using (var doc = WordprocessingDocument.Open(stream, true))
        {
            var mainPart = doc.MainDocumentPart ?? throw new InvalidOperationException("MainDocumentPart is null");
            var body = mainPart.Document?.Body;
            if (body == null) return docxBytes;

            // Replace <<ContentToBeApproved>> with HTML (table, bold, italic, list) when content is HTML
            if (useHtmlForContent)
            {
                ReplaceContentToBeApprovedWithHtml(mainPart, body, htmlContent!);
            }

            // Replace text placeholders in all parts (main, headers, footers)
            ReplaceInPart(mainPart, replacements, signatureImageBytes);

            foreach (var headerPart in mainPart.HeaderParts)
            {
                ReplaceInPart(headerPart, replacements, signatureImageBytes);
            }
            foreach (var footerPart in mainPart.FooterParts)
            {
                ReplaceInPart(footerPart, replacements, signatureImageBytes);
            }
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Replaces approval-step placeholders for electronic signing.
    /// Placeholders: &lt;&lt;Sign{NN}&gt;&gt; (image), &lt;&lt;FullName{NN}&gt;&gt;, &lt;&lt;NoteContent{NN}&gt;&gt;.
    /// Text replacements preserve template RunProperties (same behaviour as &lt;&lt;PreparedFullName&gt;&gt;).
    /// </summary>
    public static byte[] ReplaceApprovalPlaceholders(
        byte[] docxBytes,
        int stepOrder,
        byte[]? signatureImageBytes,
        string fullName,
        string noteContent)
    {
        var suffix = stepOrder.ToString("D2");
        var notePlainText = HtmlToPlainWithLineBreaks(noteContent ?? string.Empty);
        var replacements = new (string Placeholder, string Value)[]
        {
            ($"<<FullName{suffix}>>", fullName),
            ($"<<NoteContent{suffix}>>", notePlainText),
        };

        return ReplaceApprovalPlaceholdersInternal(
            docxBytes,
            replacements,
            signatureImageBytes,
            $"<<Sign{suffix}>>");
    }

    /// <summary>
    /// Replaces name and note placeholders only; keeps &lt;&lt;Sign{NN}&gt;&gt; for digital CA/BnnSoft.
    /// </summary>
    public static byte[] ReplaceApprovalNameAndNotePlaceholders(
        byte[] docxBytes,
        int stepOrder,
        string fullName,
        string noteContent)
    {
        var suffix = stepOrder.ToString("D2");
        var notePlainText = HtmlToPlainWithLineBreaks(noteContent ?? string.Empty);
        var replacements = new (string Placeholder, string Value)[]
        {
            ($"<<FullName{suffix}>>", fullName),
            ($"<<NoteContent{suffix}>>", notePlainText),
        };

        return ReplaceApprovalPlaceholdersInternal(docxBytes, replacements, signatureImageBytes: null, signImagePlaceholder: null);
    }

    private static byte[] ReplaceApprovalPlaceholdersInternal(
        byte[] docxBytes,
        (string Placeholder, string Value)[] textReplacements,
        byte[]? signatureImageBytes,
        string? signImagePlaceholder)
    {
        using var stream = new MemoryStream(docxBytes.Length);
        stream.Write(docxBytes, 0, docxBytes.Length);
        stream.Position = 0;

        using (var doc = WordprocessingDocument.Open(stream, true))
        {
            var mainPart = doc.MainDocumentPart ?? throw new InvalidOperationException("MainDocumentPart is null");
            var body = mainPart.Document?.Body;
            if (body == null) return docxBytes;

            ReplaceInPartForApproval(mainPart, mainPart, textReplacements, signatureImageBytes, signImagePlaceholder);

            foreach (var headerPart in mainPart.HeaderParts)
            {
                ReplaceInPartForApproval(mainPart, headerPart, textReplacements, signatureImageBytes, signImagePlaceholder);
            }

            foreach (var footerPart in mainPart.FooterParts)
            {
                ReplaceInPartForApproval(mainPart, footerPart, textReplacements, signatureImageBytes, signImagePlaceholder);
            }
        }

        return stream.ToArray();
    }

    private static void ReplaceInPartForApproval(
        MainDocumentPart mainPart,
        OpenXmlPart part,
        (string Placeholder, string Value)[] textReplacements,
        byte[]? signatureImageBytes,
        string? signImagePlaceholder)
    {
        OpenXmlElement? root = part switch
        {
            MainDocumentPart mdp => mdp.Document?.Body,
            HeaderPart hp => hp.Header,
            FooterPart fp => fp.Footer,
            _ => part.RootElement
        };
        if (root == null) return;

        ReplaceTextPlaceholdersInPart(root, textReplacements);

        if (signatureImageBytes != null && signatureImageBytes.Length > 0
            && !string.IsNullOrEmpty(signImagePlaceholder))
        {
            ReplaceImagePlaceholder(mainPart, root, signatureImageBytes, signImagePlaceholder);
        }
    }

    /// <summary>
    /// Replaces &lt;&lt;ContentToBeApproved&gt;&gt; paragraph with HTML-converted content (tables, bold, italic, lists).
    /// Uses HtmlToOpenXml to preserve formatting.
    /// </summary>
    private static void ReplaceContentToBeApprovedWithHtml(MainDocumentPart mainPart, Body body, string htmlContent)
    {
        var placeholder = "<<ContentToBeApproved>>";
        var paragraph = body.Descendants<Paragraph>()
            .FirstOrDefault(p => p.Descendants<Text>().Any(t => t.Text?.Contains(placeholder) == true));
        if (paragraph == null) return;

        var converter = new HtmlConverter(mainPart);
        IList<DocumentFormat.OpenXml.OpenXmlCompositeElement> parsedElements;
        try
        {
            parsedElements = converter.Parse(htmlContent);
        }
        catch
        {
            return;
        }

        if (parsedElements == null || parsedElements.Count == 0) return;

        var nextSibling = paragraph.NextSibling();
        paragraph.Remove();

        // InsertBefore inserts before the reference; first insert goes before nextSibling.
        // To preserve order, insert in reverse so the last parsed element is inserted first (ends up last).
        for (var i = parsedElements.Count - 1; i >= 0; i--)
        {
            body.InsertBefore(parsedElements[i].CloneNode(true), nextSibling);
        }
    }

    private static void ReplaceInPart(
        OpenXmlPart part,
        (string Placeholder, string Value)[] textReplacements,
        byte[]? signatureImageBytes)
    {
        OpenXmlElement? root = part switch
        {
            MainDocumentPart mdp => mdp.Document?.Body,
            HeaderPart hp => hp.Header,
            FooterPart fp => fp.Footer,
            _ => part.RootElement
        };
        if (root == null) return;

        // Replace text placeholders - use paragraph-level to handle placeholders spanning multiple runs
        ReplaceTextPlaceholdersInPart(root, textReplacements);

        // Replace <<PreparedBySign>> with image
        if (signatureImageBytes != null && signatureImageBytes.Length > 0 && part is MainDocumentPart mainPart)
        {
            ReplaceImagePlaceholder(mainPart, root, signatureImageBytes, PreparedBySignPlaceholder);
        }
    }

    /// <summary>
    /// Replace text placeholders at paragraph level to handle placeholders that span multiple Run/Text elements.
    /// Keeps existing RunProperties from the template (font, size, bold, etc.).
    /// </summary>
    private static void ReplaceTextPlaceholdersInPart(OpenXmlElement root, (string Placeholder, string Value)[] textReplacements)
    {
        foreach (var paragraph in root.Descendants<Paragraph>())
        {
            var allTexts = paragraph.Descendants<Text>().ToList();
            if (allTexts.Count == 0) continue;

            var fullTextBuilder = new StringBuilder();
            foreach (var textNode in allTexts)
            {
                var text = textNode.Text;
                if (!string.IsNullOrEmpty(text))
                {
                    fullTextBuilder.Append(text);
                }
            }

            if (fullTextBuilder.Length == 0) continue;
            var fullText = fullTextBuilder.ToString();

            string? modifiedFullText = null;
            foreach (var (placeholder, value) in textReplacements)
            {
                if (fullText.IndexOf(placeholder, StringComparison.Ordinal) >= 0)
                {
                    modifiedFullText ??= fullText;
                    modifiedFullText = modifiedFullText.Replace(placeholder, value);
                }
            }

            if (modifiedFullText == null) continue;

            // Put modified text back with line breaks preserved (Break elements in Word)
            if (allTexts.Count > 0)
            {
                if (modifiedFullText.IndexOf('\n') >= 0)
                {
                    ReplaceParagraphContentWithMultiline(paragraph, allTexts, modifiedFullText);
                }
                else
                {
                    allTexts[0].Text = modifiedFullText;
                    for (var i = 1; i < allTexts.Count; i++)
                    {
                        allTexts[i].Text = string.Empty;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Replaces paragraph content with multiline text using Break elements for line breaks.
    /// </summary>
    private static void ReplaceParagraphContentWithMultiline(Paragraph paragraph, System.Collections.Generic.List<Text> allTexts, string multilineText)
    {
        var lines = multilineText.Split(NewLineSeparator, StringSplitOptions.None);
        var templateRunProps = CloneRunPropertiesFromText(allTexts[0]);

        // Remove content elements (Run, Hyperlink, etc.) but keep ParagraphProperties
        var contentElements = paragraph.ChildElements.Where(c => c is not ParagraphProperties).ToList();
        foreach (var el in contentElements)
        {
            el.Remove();
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var run = CreateRunWithTemplateProperties(templateRunProps, lines[i]);
            if (i < lines.Length - 1)
            {
                run.AppendChild(new Break());
            }
            paragraph.AppendChild(run);
        }
    }

    private static RunProperties? CloneRunPropertiesFromText(Text textNode)
    {
        if (textNode.Parent is not Run run || run.RunProperties == null)
        {
            return null;
        }

        return (RunProperties)run.RunProperties.CloneNode(true);
    }

    private static Run CreateRunWithTemplateProperties(RunProperties? templateRunProps, string text)
    {
        var run = new Run();
        if (templateRunProps != null)
        {
            run.AppendChild((RunProperties)templateRunProps.CloneNode(true));
        }

        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    /// <summary>
    /// Replaces a signature placeholder with an inline image. Word often splits the tag across
    /// multiple &lt;w:t&gt; nodes; per-run substring removal can leave fragments.
    /// </summary>
    private static void ReplaceImagePlaceholder(
        MainDocumentPart mainPart,
        OpenXmlElement root,
        byte[] imageBytes,
        string placeholderTag)
    {
        foreach (var paragraph in root.Descendants<Paragraph>())
        {
            var allTexts = paragraph.Descendants<Text>().ToList();
            if (allTexts.Count == 0) continue;

            var fullTextBuilder = new StringBuilder();
            foreach (var textNode in allTexts)
            {
                if (!string.IsNullOrEmpty(textNode.Text))
                {
                    fullTextBuilder.Append(textNode.Text);
                }
            }

            if (fullTextBuilder.Length == 0) continue;
            var fullText = fullTextBuilder.ToString();
            var idx = fullText.IndexOf(placeholderTag, StringComparison.Ordinal);
            if (idx < 0) continue;

            var prefix = fullText.Substring(0, idx);
            var suffix = fullText.Substring(idx + placeholderTag.Length);
            var templateRunProps = allTexts
                .Select(CloneRunPropertiesFromText)
                .FirstOrDefault(rp => rp != null);

            var contentType = GetImageContentType(imageBytes);
            var imagePart = mainPart.AddImagePart(contentType);
            using (var imgStream = new MemoryStream(imageBytes))
            {
                imagePart.FeedData(imgStream);
            }
            var relationshipId = mainPart.GetIdOfPart(imagePart);
            var (emuWidth, emuHeight) = ResolveImageExtentEmu(imageBytes);
            var drawing = CreateInlineImageDrawing(relationshipId, contentType, emuWidth, emuHeight);
            var imageRun = new Run(drawing);

            var toRemove = paragraph.ChildElements.Where(c => c is not ParagraphProperties).ToList();
            foreach (var el in toRemove)
            {
                el.Remove();
            }

            if (!string.IsNullOrEmpty(prefix))
            {
                paragraph.AppendChild(CreateRunWithTemplateProperties(templateRunProps, prefix));
            }
            paragraph.AppendChild(imageRun);
            if (!string.IsNullOrEmpty(suffix))
            {
                paragraph.AppendChild(CreateRunWithTemplateProperties(templateRunProps, suffix));
            }

            return;
        }
    }

    /// <summary>
    /// Legacy wrapper for prepared-by signature placeholder.
    /// </summary>
    private static void ReplaceImagePlaceholder(MainDocumentPart mainPart, OpenXmlElement root, byte[] imageBytes)
    {
        ReplaceImagePlaceholder(mainPart, root, imageBytes, PreparedBySignPlaceholder);
    }

    private static string GetImageContentType(byte[] imageBytes)
    {
        if (imageBytes.Length >= 8)
        {
            // PNG signature
            if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && imageBytes[2] == 0x4E)
                return "image/png";
            // JPEG signature
            if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8)
                return "image/jpeg";
            // GIF
            if (imageBytes[0] == 0x47 && imageBytes[1] == 0x49)
                return "image/gif";
        }
        return "image/jpeg"; 
    }

    private static (long Width, long Height) ResolveImageExtentEmu(byte[] imageBytes)
    {
        try
        {
            var info = Image.Identify(imageBytes);
            if (info != null && info.Width > 0 && info.Height > 0)
            {
                var aspect = (double)info.Width / info.Height;
                var width = SignatureImageMaxWidthEmu;
                var height = (long)Math.Round(width / aspect);
                return (width, Math.Max(height, 1));
            }
        }
        catch
        {
            // Fall back to layout banner aspect ratio (~168:69).
        }

        return (SignatureImageMaxWidthEmu, SignatureImageFallbackHeightEmu);
    }

    private static DocumentFormat.OpenXml.Wordprocessing.Drawing CreateInlineImageDrawing(
        string relationshipId,
        string contentType,
        long emuWidth,
        long emuHeight)
    {
        var ext = contentType.Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
        return new DocumentFormat.OpenXml.Wordprocessing.Drawing(
            new DW.Inline(
                new DW.Extent { Cx = emuWidth, Cy = emuHeight },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = 1U, Name = "Signature" },
                new DW.NonVisualGraphicFrameDrawingProperties(
                    new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = 0U, Name = "signature" + ext },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip(
                                    new A.BlipExtensionList(
                                        new A.BlipExtension { Uri = "{28A0092B-C50C-407E-A947-70E740481C1C}" }))
                                {
                                    Embed = relationshipId,
                                    CompressionState = A.BlipCompressionValues.Print
                                },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = emuWidth, Cy = emuHeight }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))
                    )
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })
            )
            {
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U
            });
    }

    /// <summary>
    /// Converts HTML to plain text preserving line breaks (br, p). Bold/italic tags are stripped but content kept.
    /// </summary>
    private static string HtmlToPlainWithLineBreaks(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var opt = RegexOptions.IgnoreCase;
        var text = Regex.Replace(html, @"<br\s*/?>", "\n", opt);
        text = Regex.Replace(text, @"</p>\s*<p[^>]*>", "\n", opt);
        text = Regex.Replace(text, @"<p[^>]*>", "\n", opt);
        text = Regex.Replace(text, @"</p>", "\n", opt);
        text = Regex.Replace(text, @"<[^>]*>", " ");
        text = Regex.Replace(text, @"[^\S\n]+", " ");
        return text.Trim();
    }
}
