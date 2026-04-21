using System;
using System.Threading.Tasks;
using HC.Blazor.Hubs;
using HC.Documents;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace HC.Blazor.EventHandlers;

/// <summary>
/// Forwards long-running document operation progress (from API/worker via RabbitMQ) to the user's SignalR group.
/// </summary>
public class DocumentBackgroundOperationProgressEventHandler :
    IDistributedEventHandler<DocumentBackgroundOperationProgressEto>,
    ITransientDependency
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<DocumentBackgroundOperationProgressEventHandler> _logger;

    public DocumentBackgroundOperationProgressEventHandler(
        IHubContext<NotificationHub> hubContext,
        ILogger<DocumentBackgroundOperationProgressEventHandler> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task HandleEventAsync(DocumentBackgroundOperationProgressEto eventData)
    {
        try
        {
            await _hubContext.Clients
                .Group($"user-{eventData.UserId}")
                .SendAsync("ReceiveDocumentOperationProgress", eventData);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to push document operation progress to SignalR. OperationId={OperationId}",
                eventData.OperationId);
        }
    }
}
