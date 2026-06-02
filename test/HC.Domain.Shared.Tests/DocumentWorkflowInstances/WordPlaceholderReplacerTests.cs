using System.IO;
using DocumentFormat.OpenXml;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HC.DocumentWorkflowInstances;
using Xunit;

namespace HC.Domain.Shared.Tests.DocumentWorkflowInstances;

public class WordPlaceholderReplacerTests
{
    [Fact]
    public void ReplaceApprovalPlaceholders_ReplacesFullNameAndKeepsFontRun()
    {
        var docxBytes = CreateDocxWithParagraph("Signer: <<FullName01>>");
        var result = WordPlaceholderReplacer.ReplaceApprovalPlaceholders(
            docxBytes,
            stepOrder: 1,
            signatureImageBytes: null,
            fullName: "Nguyen Van A",
            noteContent: string.Empty);

        using var stream = new MemoryStream(result);
        using var doc = WordprocessingDocument.Open(stream, false);
        var bodyText = doc.MainDocumentPart!.Document!.Body!.InnerText;
        Assert.Contains("Nguyen Van A", bodyText);
        Assert.DoesNotContain("<<FullName01>>", bodyText);

        var runFonts = doc.MainDocumentPart.Document.Body
            .Descendants<RunProperties>()
            .Select(rp => rp.RunFonts?.Ascii)
            .FirstOrDefault(f => !string.IsNullOrEmpty(f));
        Assert.Equal("Times New Roman", runFonts);
    }

    [Fact]
    public void ReplaceApprovalNameAndNotePlaceholders_DoesNotReplaceSignTag()
    {
        var docxBytes = CreateDocxWithParagraph("<<Sign01>> <<FullName01>> <<NoteContent01>>");
        var result = WordPlaceholderReplacer.ReplaceApprovalNameAndNotePlaceholders(
            docxBytes,
            stepOrder: 1,
            fullName: "Tran Thi B",
            noteContent: "Approved");

        using var stream = new MemoryStream(result);
        using var doc = WordprocessingDocument.Open(stream, false);
        var bodyText = doc.MainDocumentPart!.Document!.Body!.InnerText;
        Assert.Contains("<<Sign01>>", bodyText);
        Assert.Contains("Tran Thi B", bodyText);
        Assert.Contains("Approved", bodyText);
    }

    private static byte[] CreateDocxWithParagraph(string text)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var runProps = new RunProperties(
                new RunFonts
                {
                    Ascii = "Times New Roman",
                    HighAnsi = "Times New Roman"
                },
                new FontSize { Val = "24" });
            var paragraph = new Paragraph(
                new Run(runProps, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
            mainPart.Document.Body!.AppendChild(paragraph);
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }
}
