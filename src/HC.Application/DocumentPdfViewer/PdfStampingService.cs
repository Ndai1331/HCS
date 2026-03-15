using System;
using System.IO;
using Microsoft.Extensions.Logging;
using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;
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

    // Watermark opacity (0-255, lower = more transparent)
    private const int WatermarkAlpha = 80;
    private const double WatermarkFontSize = 24;
    private const double DiagonalAngleDegrees = -45;

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

            var font = new XFont("Helvetica", WatermarkFontSize);
            // Red watermark for visibility (RGB 220, 0, 0)
            var brush = new XSolidBrush(XColor.FromArgb(WatermarkAlpha, 220, 0, 0));

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
}
