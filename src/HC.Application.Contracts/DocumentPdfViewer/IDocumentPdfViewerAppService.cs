using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HC.DocumentPdfViewer;

/// <summary>
/// Input for GetWatermarkedPdfAsync
/// </summary>
public class GetWatermarkedPdfInput
{
    public string BlobPath { get; set; } = string.Empty;
    public string WatermarkAction { get; set; } = "view"; // "view" or "download"
}

/// <summary>
/// Application service for viewing/downloading PDF documents with audit watermark.
/// Returns watermarked PDF bytes (user + timestamp stamped on each page).
/// MinIO keeps original file; this service generates stamped copy per request.
/// </summary>
public interface IDocumentPdfViewerAppService : IApplicationService
{
    /// <summary>
    /// Get PDF with watermark for view or download.
    /// Watermark: "Surname Name - dd/MM/yyyy HH:mm (view|download)"
    /// </summary>
    Task<byte[]> GetWatermarkedPdfAsync(GetWatermarkedPdfInput input);
}
