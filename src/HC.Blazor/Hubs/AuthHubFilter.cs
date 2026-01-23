using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authentication;

namespace HC.Blazor.Hubs;

public class ForceLogoutHubFilter : IHubFilter
{
     public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext context,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var http = context.Context.GetHttpContext();

        if (http != null)
        {
            var auth = await http.AuthenticateAsync();

            if (!auth.Succeeded)
            {
                context.Context.Abort();
                return null;
            }
        }

        return await next(context);
    }
}