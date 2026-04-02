using System;
using System.IO;
using System.Linq;
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
}

public class PdfStampingService : IPdfStampingService, ITransientDependency
{
    private readonly ILogger<PdfStampingService> _logger;

    // Watermark opacity (0-255, lower = more transparent / fainter)
    private const int WatermarkAlpha = 48;
    private const double WatermarkFontSize = 18;
    private const double DiagonalAngleDegrees = -45;
    private static readonly string[] WatermarkFontCandidates =
    {
        "Helvetica",
        "Arial",
        "DejaVu Sans",
        "Liberation Sans",
        "Noto Sans",
        "FreeSans"
    };

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
                    string.Join(", ", WatermarkFontCandidates));
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
        foreach (var fontName in WatermarkFontCandidates)
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
}
