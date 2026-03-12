using System;
using HC.Localization;
using HC.Blazor.Services;
using Volo.Abp.AspNetCore.Components.Messages;
using Volo.Abp.Authorization;
using Volo.Abp.AspNetCore.Components;
using Volo.Abp.Http.Client;

namespace HC.Blazor;

public abstract class HCComponentBase : AbpComponentBase
{
    private const string ForbiddenErrorCode = "Volo.Authorization:010001";

    [Microsoft.AspNetCore.Components.Inject]
    protected IUiMessageService UiMessageService { get; set; } = default!;

    [Microsoft.AspNetCore.Components.Inject]
    protected GlobalExceptionHandler GlobalExceptionHandler { get; set; } = default!;

    protected HCComponentBase()
    {
        LocalizationResource = typeof(HCResource);
    }

    protected override async System.Threading.Tasks.Task HandleErrorAsync(Exception exception)
    {
        // Redirect to login when session expired (Unauthorized)
        if (await TryHandleUnauthorizedErrorAsync(exception))
        {
            return;
        }

        if (await TryHandleForbiddenErrorAsync(exception))
        {
            return;
        }

        await base.HandleErrorAsync(exception);
    }

    private async System.Threading.Tasks.Task<bool> TryHandleUnauthorizedErrorAsync(Exception exception)
    {
        if (!IsUnauthorizedError(exception))
        {
            return false;
        }

        await GlobalExceptionHandler.HandleAuthErrorExceptionAsync(exception);
        return true;
    }

    private static bool IsUnauthorizedError(Exception exception)
    {
        if (exception is AbpRemoteCallException remoteException)
        {
            var msg = remoteException.Message ?? string.Empty;
            return msg.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("401", StringComparison.Ordinal)
                   || msg.Contains("Token", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("Authentication", StringComparison.OrdinalIgnoreCase);
        }

        return exception.InnerException != null && IsUnauthorizedError(exception.InnerException);
    }

    private async System.Threading.Tasks.Task<bool> TryHandleForbiddenErrorAsync(Exception exception)
    {
        if (!IsForbiddenError(exception))
        {
            return false;
        }

        await UiMessageService.Error(L["Forbidden"],
            options: new System.Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
        return true;
    }

    private static bool IsForbiddenError(Exception exception)
    {
        if (exception is AbpAuthorizationException)
        {
            return true;
        }

        if (exception is AbpRemoteCallException remoteException)
        {
            if (string.Equals(remoteException.Error?.Code, ForbiddenErrorCode, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(remoteException.Message)
                && remoteException.Message.Contains(ForbiddenErrorCode, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return exception.InnerException != null && IsForbiddenError(exception.InnerException);
    }
}
