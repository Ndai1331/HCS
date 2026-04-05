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
/// SignalR Hub for real-time chat messaging
/// </summary>
/// <remarks>
/// Cookie for Blazor web; Bearer JWT for mobile/native — avoids OIDC challenge (302) on negotiate.
/// </remarks>
[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme + "," + JwtBearerDefaults.AuthenticationScheme)]
public class ChatHub : Hub
{
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(ILogger<ChatHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        // Get user ID from claims
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userIdentifier = Context.UserIdentifier;

        _logger.LogInformation(
            "Chat SignalR client connected: ConnectionId={ConnectionId}, UserId={UserId}, UserIdentifier={UserIdentifier}",
            Context.ConnectionId,
            userId,
            userIdentifier);

        if (!string.IsNullOrEmpty(userId))
        {
            // Add user to group for easier management
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
            _logger.LogInformation("Added user to chat group: UserId={UserId}, ConnectionId={ConnectionId}", userId, Context.ConnectionId);
        }
        else
        {
            _logger.LogWarning("No user ID found in claims for chat connection: ConnectionId={ConnectionId}", Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        _logger.LogInformation(
            "Chat SignalR client disconnected: ConnectionId={ConnectionId}, UserId={UserId}, Exception={Exception}",
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