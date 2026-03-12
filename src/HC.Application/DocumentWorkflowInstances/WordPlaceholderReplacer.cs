using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
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
    private const long ImageEmuWidth = 990000L;  // ~2.6cm
    private const long ImageEmuHeight = 792000L;  // ~2.1cm

    /// <summary>
    /// Replace placeholders in Word document bytes. Returns modified .docx bytes.
    /// Placeholders: &lt;&lt;DD&gt;&gt;, &lt;&lt;MM&gt;&gt;, &lt;&lt;YYYY&gt;&gt;,
    /// &lt;&lt;ContentToBeApproved&gt;&gt;, &lt;&lt;PreparedBySign&gt;&gt;, &lt;&lt;PreparedFullName&gt;&gt;
    /// </summary>
    public static byte[] ReplacePlaceholders(
        byte[] docxBytes,
        byte[]? signatureImageBytes,
        string fullName,
        string htmlContent,
        DateTime currentDate)
    {
        var plainContent = HtmlToPlainWithLineBreaks(htmlContent ?? string.Empty);
        var replacements = new (string Placeholder, string Value)[]
        {
            ("<<DD>>", currentDate.ToString("dd")),
            ("<<MM>>", currentDate.ToString("MM")),
            ("<<YYYY>>", currentDate.ToString("yyyy")),
            ("<<ContentToBeApproved>>", plainContent),
            ("<<PreparedFullName>>", fullName),
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
            ReplaceImagePlaceholder(mainPart, root, signatureImageBytes);
        }
    }

    /// <summary>
    /// Replace text placeholders at paragraph level to handle placeholders that span multiple Run/Text elements.
    /// Preserves line breaks in replacement values (e.g. ContentToBeApproved with multiple lines).
    /// </summary>
    private static void ReplaceTextPlaceholdersInPart(OpenXmlElement root, (string Placeholder, string Value)[] textReplacements)
    {
        foreach (var paragraph in root.Descendants<Paragraph>())
        {
            var allTexts = paragraph.Descendants<Text>().ToList();
            var fullText = string.Concat(allTexts.Select(t => t.Text));
            if (string.IsNullOrEmpty(fullText)) continue;

            string? modifiedFullText = null;
            foreach (var (placeholder, value) in textReplacements)
            {
                if (fullText.Contains(placeholder))
                {
                    modifiedFullText ??= fullText;
                    modifiedFullText = modifiedFullText.Replace(placeholder, value);
                }
            }

            if (modifiedFullText == null) continue;

            // Put modified text back with line breaks preserved (Break elements in Word)
            if (allTexts.Count > 0)
            {
                if (modifiedFullText.Contains('\n'))
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
        var lines = multilineText.Split('\n');

        // Remove content elements (Run, Hyperlink, etc.) but keep ParagraphProperties
        var contentElements = paragraph.ChildElements.Where(c => c is not ParagraphProperties).ToList();
        foreach (var el in contentElements)
        {
            el.Remove();
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var run = new Run(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
            if (i < lines.Length - 1)
            {
                run.AppendChild(new Break());
            }
            paragraph.AppendChild(run);
        }
    }

    private static void ReplaceImagePlaceholder(MainDocumentPart mainPart, OpenXmlElement root, byte[] imageBytes)
    {
        // All possible substrings when placeholder is split across runs/paragraphs (longest first)
        var toRemove = new[]
        {
            "<<PreparedBySign>>", "<<PreparedBySign", "PreparedBySign>>",
            "<<PreparedBy", "BySign>>", "<<Prepared", "paredBySign>>",
            "Sign>>", "<<Pre", "paredBy"
        };

        OpenXmlElement? insertBeforeElement = null;
        OpenXmlElement? parentForInsert = null;

        // Process ALL Text elements in the part (not just per-paragraph) to ensure we don't miss any
        foreach (var textElem in root.Descendants<Text>().ToList())
        {
            var t = textElem.Text;
            var hadPlaceholder = toRemove.Any(p => t.Contains(p));
            if (!hadPlaceholder) continue;

            foreach (var p in toRemove)
            {
                t = t.Replace(p, string.Empty);
            }
            textElem.Text = t;

            // First Text that had placeholder: find its Run and paragraph for insertion
            if (insertBeforeElement == null)
            {
                var run = textElem.Parent as Run;
                if (run != null)
                {
                    var para = run.Ancestors<Paragraph>().FirstOrDefault();
                    if (para != null)
                    {
                        // Find direct child of paragraph (Run or Hyperlink) that contains this run
                        OpenXmlElement directChild = run;
                        while (directChild.Parent != null && directChild.Parent != para)
                        {
                            directChild = directChild.Parent;
                        }
                        parentForInsert = para;
                        insertBeforeElement = directChild;
                    }
                }
            }
        }

        if (parentForInsert == null) return;

        // Add image part and create Drawing
        var contentType = GetImageContentType(imageBytes);
        var imagePart = mainPart.AddImagePart(contentType);
        using (var imgStream = new MemoryStream(imageBytes))
        {
            imagePart.FeedData(imgStream);
        }
        var relationshipId = mainPart.GetIdOfPart(imagePart);
        var drawing = CreateInlineImageDrawing(relationshipId, contentType);

        var imageRun = new Run(drawing);
        if (insertBeforeElement != null)
        {
            parentForInsert.InsertBefore(imageRun, insertBeforeElement);
        }
        else
        {
            parentForInsert.AppendChild(imageRun);
        }
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

    private static DocumentFormat.OpenXml.Wordprocessing.Drawing CreateInlineImageDrawing(string relationshipId, string contentType)
    {
        var ext = contentType.Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
        return new DocumentFormat.OpenXml.Wordprocessing.Drawing(
            new DW.Inline(
                new DW.Extent { Cx = ImageEmuWidth, Cy = ImageEmuHeight },
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
                                    new A.Extents { Cx = ImageEmuWidth, Cy = ImageEmuHeight }),
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
