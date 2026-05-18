using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HC;
using Microsoft.Extensions.Logging;
using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;
using System.Runtime.InteropServices;
using Volo.Abp.DependencyInjection;
using XGraphicsPdfPageOptions = PdfSharp.Drawing.XGraphicsPdfPageOptions;

namespace HC.DocumentPdfViewer;

/// <summary>
/// Service to add diagonal watermark to PDF files.
/// Watermark format: "Surname Name - DateTime" (faded, diagonal across each page).
/// Uses PdfSharp to manipulate existing PDFs.
/// </summary>
public interface IPdfStampingService
{
    /// <summary>
    /// Add diagonal watermark to PDF bytes. Watermark text: "userDisplayName - actionTime".
    /// </summary>
    /// <param name="pdfBytes">Original PDF bytes from MinIO</param>
    /// <param name="userDisplayName">User display name (e.g. Surname + " " + Name)</param>
    /// <param name="actionTime">When the view/download action occurred</param>
    /// <param name="action">"view" or "download" for audit trail</param>
    /// <returns>PDF bytes with watermark on each page</returns>
    byte[] AddWatermark(byte[] pdfBytes, string userDisplayName, DateTime actionTime, string action);

    /// <summary>
    /// Add a free-text note to a specific page and coordinate in a PDF.
    /// </summary>
    byte[] AddTextNote(
        byte[] pdfBytes,
        int pageNumber,
        double pdfX,
        double pdfY,
        string noteContent,
        byte[]? signatureImageBytes = null,
        string? signerFullName = null);
}

public class PdfStampingService : IPdfStampingService, ITransientDependency
{
    private readonly ILogger<PdfStampingService> _logger;

    // Watermark opacity (0-255, lower = more transparent / fainter)
    private const int WatermarkAlpha = 48;
    private const double WatermarkFontSize = 18;
    private const double NoteFontSize = 9.5;
    private const double SignerNameFontSize = 8.5;
    private const double SignatureTopSpacing = 6;
    private const double SignatureBottomSpacing = 3;
    private const double SignatureMaxWidth = 110;
    private const double SignatureMaxHeight = 42;
    private const double DiagonalAngleDegrees = -45;

    private static string PrimaryPdfFontFamily => PdfFontEnvironment.DefaultPdfFontFamily;

    private static string[] GetWatermarkFontCandidates()
    {
        var primary = PdfFontEnvironment.DefaultPdfFontFamily;
        var ordered = new List<string> { primary };
        foreach (var name in new[] { "Liberation Sans", "Helvetica", "Arial", "DejaVu Sans", "Noto Sans", "FreeSans" })
        {
            if (!ordered.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                ordered.Add(name);
            }
        }

        return ordered.ToArray();
    }

    public PdfStampingService(ILogger<PdfStampingService> logger)
    {
        _logger = logger;
    }

    public byte[] AddWatermark(byte[] pdfBytes, string userDisplayName, DateTime actionTime, string action)
    {
        if (pdfBytes == null || pdfBytes.Length == 0)
        {
            return pdfBytes ?? Array.Empty<byte>();
        }

        var watermarkText = $"{userDisplayName} - {actionTime:dd/MM/yyyy HH:mm} ({action})";

        try
        {
            using var inputStream = new MemoryStream(pdfBytes);
            var document = PdfReader.Open(inputStream, PdfDocumentOpenMode.Modify);

            var font = ResolveWatermarkFont();
            if (font == null)
            {
                _logger.LogWarning(
                    "No usable font found for PDF watermark. Candidates: {Candidates}. Returning original PDF.",
                    string.Join(", ", GetWatermarkFontCandidates()));
                LogRuntimeFontDiagnostics();
                return pdfBytes;
            }
            // Subtle gray watermark (low alpha for a dim appearance)
            var brush = new XSolidBrush(XColor.FromArgb(WatermarkAlpha, 110, 110, 115));

            for (var i = 0; i < document.PageCount; i++)
            {
                var page = document.Pages[i];
                var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

                try
                {
                    // Page dimensions (use Point for PDFsharp 6.1+)
                    var pageWidth = page.Width.Point;
                    var pageHeight = page.Height.Point;
                    var centerX = pageWidth / 2;
                    var centerY = pageHeight / 2;

                    // Save state, apply transform for diagonal text
                    gfx.Save();
                    gfx.TranslateTransform(centerX, centerY);
                    gfx.RotateTransform(DiagonalAngleDegrees);
                    gfx.TranslateTransform(-centerX, -centerY);

                    // Measure text to center it (XSize.Width/Height are in points)
                    var size = gfx.MeasureString(watermarkText, font);
                    var x = (pageWidth - size.Width) / 2;
                    var y = (pageHeight - size.Height) / 2;

                    gfx.DrawString(watermarkText, font, brush, x, y, XStringFormats.Default);
                }
                finally
                {
                    gfx.Restore();
                    gfx.Dispose();
                }
            }

            using var outputStream = new MemoryStream();
            document.Save(outputStream, false);
            return outputStream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add watermark to PDF. Returning original.");
            return pdfBytes;
        }
    }

    public byte[] AddTextNote(
        byte[] pdfBytes,
        int pageNumber,
        double pdfX,
        double pdfY,
        string noteContent,
        byte[]? signatureImageBytes = null,
        string? signerFullName = null)
    {
        if (pdfBytes == null || pdfBytes.Length == 0)
        {
            return pdfBytes ?? Array.Empty<byte>();
        }

        if (pageNumber <= 0 || string.IsNullOrWhiteSpace(noteContent))
        {
            return pdfBytes;
        }

        try
        {
            using var inputStream = new MemoryStream(pdfBytes);
            var document = PdfReader.Open(inputStream, PdfDocumentOpenMode.Modify);

            var pageIndex = pageNumber - 1;
            if (pageIndex < 0 || pageIndex >= document.PageCount)
            {
                _logger.LogWarning("AddTextNote invalid pageNumber={PageNumber}, pageCount={PageCount}", pageNumber, document.PageCount);
                return pdfBytes;
            }

            var page = document.Pages[pageIndex];
            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            var font = ResolveNoteFont(NoteFontSize);
            var signerNameFont = ResolveNoteFont(SignerNameFontSize);
            if (font == null || signerNameFont == null)
            {
                _logger.LogError(
                    "Failed to add text note: no usable font. Candidates tried: {Candidates}. " +
                    "On Linux set HC_PDF_FONT_ENV=production or install fonts (e.g. fonts-liberation) and optionally HC_FONT_DIRS.",
                    string.Join(", ", GetWatermarkFontCandidates()));
                LogRuntimeFontDiagnostics();
                return pdfBytes;
            }

            // pdf.js returns PDF coordinates in a bottom-left origin system.
            // PdfSharp draws using a top-left origin, so Y must be inverted.
            var normalizedY = page.Height.Point - pdfY;

            // Keep marker inside page boundaries.
            var safeX = Math.Clamp(pdfX, 8, page.Width.Point - 8);
            var safeY = Math.Clamp(normalizedY, 12, page.Height.Point - 12);

            var inkColor = XColor.FromArgb(34, 83, 185);
            var pen = new XPen(inkColor, 0.9);
            var brush = new XSolidBrush(inkColor);
            gfx.DrawEllipse(pen, brush, safeX - 2, safeY - 2, 4, 4);

            var textX = Math.Min(safeX + 6, page.Width.Point - 24);
            var textY = Math.Max(safeY - 4, 12);
            var maxTextWidth = Math.Max(80, page.Width.Point - textX - 12);
            var lineHeight = gfx.MeasureString("Ag", font).Height + 1.5;
            var noteLines = WrapNoteLines(gfx, font, noteContent, maxTextWidth);

            for (var i = 0; i < noteLines.Count; i++)
            {
                gfx.DrawString(noteLines[i], font, brush, textX, textY + (i * lineHeight), XStringFormats.TopLeft);
            }

            var renderedNoteHeight = Math.Max(1, noteLines.Count) * lineHeight;
            var signatureStartY = textY + renderedNoteHeight + SignatureTopSpacing;
            var signerName = signerFullName?.Trim();
            var signerNameHeight = string.IsNullOrWhiteSpace(signerName)
                ? 0
                : gfx.MeasureString(signerName, signerNameFont).Height;
            var remainingHeight = page.Height.Point - signatureStartY - 12;
            var remainingWidth = Math.Max(24, Math.Min(SignatureMaxWidth, maxTextWidth));
            var signerNameX = textX;
            var signerNameWidth = remainingWidth;

            if (signatureImageBytes is { Length: > 0 })
            {
                try
                {
                    var heightReservedForName = signerNameHeight > 0
                        ? signerNameHeight + SignatureBottomSpacing
                        : 0;
                    var maxImageHeight = Math.Min(SignatureMaxHeight, Math.Max(0, remainingHeight - heightReservedForName));
                    if (maxImageHeight >= 12)
                    {
                        var opaqueBytes = SignatureImageHelper.FlattenTransparency(signatureImageBytes);
                        using var imgStream = new MemoryStream(opaqueBytes);
                        using var signatureImage = XImage.FromStream(imgStream);

                        var imageAspect = (double)signatureImage.PixelWidth / Math.Max(signatureImage.PixelHeight, 1);
                        var imageWidth = remainingWidth;
                        var imageHeight = imageWidth / imageAspect;

                        if (imageHeight > maxImageHeight)
                        {
                            imageHeight = maxImageHeight;
                            imageWidth = imageHeight * imageAspect;
                        }

                        gfx.DrawImage(signatureImage, textX, signatureStartY, imageWidth, imageHeight);
                        signerNameX = textX;
                        signerNameWidth = imageWidth;
                        signatureStartY += imageHeight + SignatureBottomSpacing;
                        remainingHeight = page.Height.Point - signatureStartY - 12;
                    }
                }
                catch (Exception imgEx)
                {
                    _logger.LogError(imgEx,
                        "Failed to render signature image ({Length} bytes) in AddTextNote. " +
                        "Text note and signer name will still be rendered.",
                        signatureImageBytes.Length);
                }
            }

            if (!string.IsNullOrWhiteSpace(signerName) && remainingHeight >= signerNameHeight)
            {
                var signerNameRect = new XRect(signerNameX, signatureStartY, signerNameWidth, signerNameHeight + 2);
                gfx.DrawString(signerName, signerNameFont, XBrushes.Black, signerNameRect, XStringFormats.TopCenter);
            }

            using var outputStream = new MemoryStream();
            document.Save(outputStream, false);
            return outputStream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add text note to PDF. Returning original.");
            return pdfBytes;
        }
    }

    private void LogRuntimeFontDiagnostics()
    {
        try
        {
            var fontDirectories = GetRuntimeFontDirectories();
            foreach (var dir in fontDirectories)
            {
                if (!Directory.Exists(dir))
                {
                    _logger.LogWarning("Font diagnostic: directory not found: {Directory}", dir);
                    continue;
                }

                var files = Directory
                    .EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
                             || f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)
                             || f.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase))
                    .Take(20)
                    .ToList();

                _logger.LogWarning(
                    "Font diagnostic: directory={Directory}, sampleFontCount={Count}, sampleFonts={Fonts}",
                    dir,
                    files.Count,
                    string.Join(" | ", files.Select(Path.GetFileName)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Font diagnostic failed.");
        }
    }

    private static string[] GetRuntimeFontDirectories()
    {
        var envDirs = (Environment.GetEnvironmentVariable("HC_FONT_DIRS") ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var appFonts = new[]
        {
            Path.Combine(baseDir, "fonts"),
            "/app/fonts"
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return envDirs
                .Concat(appFonts)
                .Concat(new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts)),
                    @"C:\Windows\Fonts"
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return envDirs
                .Concat(appFonts)
                .Concat(new[]
                {
                    "/System/Library/Fonts/Supplemental",
                    "/System/Library/Fonts",
                    "/Library/Fonts",
                    Path.Combine(home, "Library/Fonts")
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        var linuxHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return envDirs
            .Concat(appFonts)
            .Concat(new[]
            {
                "/usr/share/fonts",
                "/usr/local/share/fonts",
                "/usr/share/fonts/truetype",
                Path.Combine(linuxHome, ".fonts")
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private XFont? ResolveWatermarkFont()
    {
        foreach (var fontName in GetWatermarkFontCandidates())
        {
            try
            {
                return new XFont(fontName, WatermarkFontSize);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Watermark font '{FontName}' is unavailable.", fontName);
            }
        }

        return null;
    }

    private XFont? ResolveNoteFont(double fontSize)
    {
        foreach (var fontName in GetWatermarkFontCandidates())
        {
            try
            {
                return new XFont(fontName, fontSize);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Note font '{FontName}' is unavailable.", fontName);
            }
        }

        return null;
    }

    private static System.Collections.Generic.List<string> WrapNoteLines(XGraphics gfx, XFont font, string noteContent, double maxTextWidth)
    {
        var wrappedLines = new System.Collections.Generic.List<string>();
        var paragraphs = noteContent
            .Trim()
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrWhiteSpace(paragraph))
            {
                wrappedLines.Add(string.Empty);
                continue;
            }

            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                wrappedLines.Add(string.Empty);
                continue;
            }

            var currentLine = words[0];
            var lineBuilder = new System.Text.StringBuilder(Math.Max(32, currentLine.Length + 16));
            lineBuilder.Append(currentLine);
            for (var i = 1; i < words.Length; i++)
            {
                lineBuilder.Clear();
                lineBuilder.Append(currentLine);
                lineBuilder.Append(' ');
                lineBuilder.Append(words[i]);
                var candidate = lineBuilder.ToString();
                if (gfx.MeasureString(candidate, font).Width <= maxTextWidth)
                {
                    currentLine = candidate;
                    continue;
                }

                wrappedLines.Add(currentLine);
                currentLine = words[i];
            }

            wrappedLines.Add(currentLine);
        }

        return wrappedLines;
    }
}
