using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace HC.DocumentPdfViewer;

/// <summary>
/// Returns watermarked PDF for view/download. MinIO keeps original; generates stamped copy per user/action.
/// </summary>
public class DocumentPdfViewerAppService : ApplicationService, IDocumentPdfViewerAppService
{
    private readonly IBlobContainer _blobContainer;
    private readonly IPdfStampingService _pdfStampingService;
    private readonly IRepository<Volo.Abp.Identity.IdentityUser, Guid> _identityUserRepository;
    private readonly ILogger<DocumentPdfViewerAppService> _logger;

    public DocumentPdfViewerAppService(
        IBlobContainer blobContainer,
        IPdfStampingService pdfStampingService,
        IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository,
        ILogger<DocumentPdfViewerAppService> logger)
    {
        _blobContainer = blobContainer;
        _pdfStampingService = pdfStampingService;
        _identityUserRepository = identityUserRepository;
        _logger = logger;
    }

    public async Task<byte[]> GetWatermarkedPdfAsync(GetWatermarkedPdfInput input)
    {
        var blobPath = input?.BlobPath ?? string.Empty;
        var action = input?.WatermarkAction ?? "view";
        if (string.IsNullOrWhiteSpace(blobPath))
        {
            throw new Volo.Abp.UserFriendlyException("Blob path is required.");
        }

        var actionTime = Clock.Now;
        var userDisplayName = await GetCurrentUserDisplayNameAsync();

        var pdfBytes = await _blobContainer.GetAllBytesAsync(blobPath);
        if (pdfBytes == null || pdfBytes.Length == 0)
        {
            throw new Volo.Abp.UserFriendlyException("PDF file not found or empty.");
        }

        var stampedBytes = _pdfStampingService.AddWatermark(pdfBytes, userDisplayName, actionTime, action);
        if (ReferenceEquals(stampedBytes, pdfBytes))
        {
            _logger.LogWarning(
                "PDF watermarking skipped/failed for {User} action={Action} path={Path}. Returning original bytes.",
                userDisplayName,
                action,
                blobPath);
        }
        else
        {
            _logger.LogInformation("PDF watermarked for {User} action={Action} path={Path}", userDisplayName, action, blobPath);
        }
        return stampedBytes;
    }

    private async Task<string> GetCurrentUserDisplayNameAsync()
    {
        var userId = CurrentUser.Id;
        if (!userId.HasValue)
        {
            return CurrentUser.UserName ?? "Anonymous";
        }

        var user = await _identityUserRepository.FindAsync(userId.Value);
        if (user == null)
        {
            return CurrentUser.UserName ?? "Unknown";
        }

        var parts = new[] { user.Surname, user.Name }.Where(s => !string.IsNullOrWhiteSpace(s));
        var result = string.Join(" ", parts).Trim();
        return string.IsNullOrWhiteSpace(result) ? (CurrentUser.UserName ?? "Unknown") : result;
    }
}
