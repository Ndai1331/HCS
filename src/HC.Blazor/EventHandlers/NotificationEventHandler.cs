// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Threading.Tasks;
// using HC.Notifications;
// using Microsoft.AspNetCore.SignalR;
// using Microsoft.Extensions.Logging;
// using Volo.Abp.DependencyInjection;
// using Volo.Abp.EventBus.Distributed;
// using HC.Blazor.Hubs;

// namespace HC.Blazor.EventHandlers;
// public class NotificationEventHandler :
//     IDistributedEventHandler<NotificationCreatedEto>,
//     ITransientDependency
// {
//     private readonly IHubContext<NotificationHub> _hubContext;
//     private readonly ILogger<NotificationEventHandler> _logger;

//     public NotificationEventHandler(
//         IHubContext<NotificationHub> hubContext,
//         ILogger<NotificationEventHandler> logger)
//     {
//         _hubContext = hubContext;
//         _logger = logger;
//     }

//     public async Task HandleEventAsync(NotificationCreatedEto eventData)
//     {
//         try
//         {
//             _logger.LogInformation(
//                 "Handling NotificationCreatedEto: NotificationId={NotificationId}, ReceiverCount={ReceiverCount}",
//                 eventData.NotificationId,
//                 eventData.ReceiverUserIds?.Count ?? 0);

//             if (eventData.ReceiverUserIds == null || eventData.ReceiverUserIds.Count == 0)
//             {
//                 _logger.LogWarning("No receiver user IDs in event data");
//                 return;
//             }

//             // Send notification to each receiver user
//             // SignalR uses the NameIdentifier claim to identify users
//             foreach (var userId in eventData.ReceiverUserIds)
//             {
//                 try
//                 {
//                     var userIdString = userId.ToString();
//                     _logger.LogInformation(
//                         "Attempting to send notification via SignalR: UserId={UserId}, NotificationId={NotificationId}",
//                         userIdString,
//                         eventData.NotificationId);

//                     // Send to user by their user ID (SignalR maps this to NameIdentifier claim)
//                     // SignalR's Clients.User() uses Context.UserIdentifier which comes from ClaimTypes.NameIdentifier
//                     // Only send once - Clients.User() is sufficient since user is also in the group
//                     await _hubContext.Clients
//                         .User(userIdString)
//                         .SendAsync("ReceiveNotification", eventData.NotificationId);

//                     _logger.LogInformation(
//                         "Successfully sent notification to user: UserId={UserId}, NotificationId={NotificationId}",
//                         userIdString,
//                         eventData.NotificationId);
//                 }
//                 catch (Exception ex)
//                 {
//                     _logger.LogError(ex,
//                         "Failed to send notification to user: UserId={UserId}, NotificationId={NotificationId}, Error={Error}",
//                         userId,
//                         eventData.NotificationId,
//                         ex.Message);
//                 }
//             }
//         }
//         catch (Exception ex)
//         {
//             _logger.LogError(ex,
//                 "Error handling NotificationCreatedEto: NotificationId={NotificationId}",
//                 eventData.NotificationId);
//         }
//     }
// }
