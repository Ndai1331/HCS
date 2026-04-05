using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.Logging;

namespace HC.Blazor.Hubs;

/// <summary>
/// SignalR Hub for real-time notifications
/// </summary>
[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme + "," + JwtBearerDefaults.AuthenticationScheme)]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        // Get user ID from claims
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userIdentifier = Context.UserIdentifier;
        
        _logger.LogInformation(
            "SignalR client connected: ConnectionId={ConnectionId}, UserId={UserId}, UserIdentifier={UserIdentifier}",
            Context.ConnectionId,
            userId,
            userIdentifier);

        if (!string.IsNullOrEmpty(userId))
        {
            // Add user to group for easier management (optional)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
            _logger.LogInformation("Added user to group: UserId={UserId}, ConnectionId={ConnectionId}", userId, Context.ConnectionId);
        }
        else
        {
            _logger.LogWarning("No user ID found in claims for connection: ConnectionId={ConnectionId}", Context.ConnectionId);
        }
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        _logger.LogInformation(
            "SignalR client disconnected: ConnectionId={ConnectionId}, UserId={UserId}, Exception={Exception}",
            Context.ConnectionId,
            userId,
            exception?.Message);

        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
        }
        
        await base.OnDisconnectedAsync(exception);
    }
}
