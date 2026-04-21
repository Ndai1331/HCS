using System;
using System.Text.Json;
using System.Threading.Tasks;
using HC.Documents;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp;

namespace HC.Documents.BackgroundJobs;

/// <summary>
/// Runs approve-with-note in a background worker scope and publishes progress to SignalR via distributed events.
/// </summary>
public class ApproveWithNoteJobExecutor : ITransientDependency
{
    private readonly IRepository<DocumentBackgroundOperation, Guid> _operationRepo;
    private readonly IDocumentRepository _documentRepo;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly DocumentsAppService _documentsAppService;
    private readonly ILogger<ApproveWithNoteJobExecutor> _logger;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IdentityUserManager _identityUserManager;
    private readonly IUserClaimsPrincipalFactory<IdentityUser> _userClaimsPrincipalFactory;

    public ApproveWithNoteJobExecutor(
        IRepository<DocumentBackgroundOperation, Guid> operationRepo,
        IDocumentRepository documentRepo,
        ICurrentTenant currentTenant,
        IDistributedEventBus distributedEventBus,
        DocumentsAppService documentsAppService,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IdentityUserManager identityUserManager,
        IUserClaimsPrincipalFactory<IdentityUser> userClaimsPrincipalFactory,
        ILogger<ApproveWithNoteJobExecutor> logger)
    {
        _operationRepo = operationRepo;
        _documentRepo = documentRepo;
        _currentTenant = currentTenant;
        _distributedEventBus = distributedEventBus;
        _documentsAppService = documentsAppService;
        _currentPrincipalAccessor = currentPrincipalAccessor;
        _identityUserManager = identityUserManager;
        _userClaimsPrincipalFactory = userClaimsPrincipalFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid operationId)
    {
        var op = await _operationRepo.GetAsync(operationId);
        using (_currentTenant.Change(op.TenantId))
        {
            // Resolve document info once so every progress event carries No/Title for the UI toast.
            string? docNo = null;
            string? docTitle = null;
            if (op.DocumentId.HasValue)
            {
                try
                {
                    var doc = await _documentRepo.FindAsync(op.DocumentId.Value);
                    docNo = doc?.No;
                    docTitle = doc?.Title;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Cannot load document metadata for operation {OperationId}", operationId);
                }
            }

            var opTypeDisplay = BuildOperationTypeDisplay(op.OperationType);

            try
            {
                op.Status = DocumentBackgroundOperationStatuses.Running;
                await _operationRepo.UpdateAsync(op);
                await PublishProgressAsync(op, DocumentBackgroundOperationStatuses.Running, 0, "Đang khởi tạo...", docNo, docTitle, opTypeDisplay);

                var input = JsonSerializer.Deserialize<ApproveDocumentWithNoteInput>(op.InputJson ?? "{}");
                if (input == null)
                {
                    throw new UserFriendlyException("Invalid operation payload.");
                }

                // No HTTP user in background scope — impersonate the user who queued the job so AppService authorization passes.
                var identityUser = await _identityUserManager.GetByIdAsync(op.UserId);
                if (identityUser == null)
                {
                    throw new UserFriendlyException($"Không tìm thấy người dùng cho tác vụ nền (Id: {op.UserId}).");
                }

                var principal = await _userClaimsPrincipalFactory.CreateAsync(identityUser);
                using (_currentPrincipalAccessor.Change(principal))
                {
                    await _documentsAppService.ExecuteApproveWithNoteForUserAsync(input, op.UserId, async (progress, message) =>
                    {
                        op.Progress = progress;
                        op.Message = message;
                        await _operationRepo.UpdateAsync(op);
                        await PublishProgressAsync(op, DocumentBackgroundOperationStatuses.Running, progress, message, docNo, docTitle, opTypeDisplay);
                    });
                }

                op.Status = DocumentBackgroundOperationStatuses.Completed;
                op.Progress = 100;
                op.Message = "Hoàn thành";
                await _operationRepo.UpdateAsync(op);
                await PublishProgressAsync(op, DocumentBackgroundOperationStatuses.Completed, 100, "Hoàn thành", docNo, docTitle, opTypeDisplay);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ApproveWithNote background job failed. OperationId={OperationId}", operationId);
                op.Status = DocumentBackgroundOperationStatuses.Failed;
                op.ErrorMessage = ex.Message;
                op.Message = "Thất bại";
                await _operationRepo.UpdateAsync(op);
                await PublishProgressAsync(op, DocumentBackgroundOperationStatuses.Failed, op.Progress, op.Message, docNo, docTitle, opTypeDisplay, ex.Message);
            }
        }
    }

    private static string BuildOperationTypeDisplay(string opType)
    {
        return opType switch
        {
            DocumentBackgroundOperationTypes.ApproveWithNote => "Phê duyệt kèm ghi chú",
            _ => opType
        };
    }

    private Task PublishProgressAsync(
        DocumentBackgroundOperation op,
        string status,
        int progress,
        string? message,
        string? documentNo,
        string? documentTitle,
        string? operationTypeDisplay,
        string? errorMessage = null)
    {
        return _distributedEventBus.PublishAsync(new DocumentBackgroundOperationProgressEto
        {
            OperationId = op.Id,
            UserId = op.UserId,
            TenantId = op.TenantId,
            OperationType = op.OperationType,
            OperationTypeDisplay = operationTypeDisplay,
            Status = status,
            Progress = progress,
            Message = message,
            ErrorMessage = errorMessage,
            DocumentId = op.DocumentId,
            DocumentNo = documentNo,
            DocumentTitle = documentTitle
        });
    }
}
