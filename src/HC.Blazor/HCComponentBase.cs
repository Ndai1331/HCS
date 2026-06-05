using System;
using System.Collections.Generic;
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
    private static readonly Dictionary<string, string> BusinessErrorLocalizationMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HC.Chat:MustTransferAdminBeforeLeaving"] = "MustTransferAdminBeforeLeaving",
        ["HC.Chat:OnlyAdminCanDeleteConversation"] = "OnlyAdminCanDeleteConversation",
        ["HC.Chat:OnlyAdminCanDeleteOthersMessages"] = "OnlyAdminCanDeleteOthersMessages"
    };

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

        if (await TryHandleBusinessErrorAsync(exception))
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
            if (remoteException.HttpStatusCode == 403)
            {
                return true;
            }

            if (string.Equals(remoteException.Error?.Code, ForbiddenErrorCode, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(remoteException.Message)
                && (remoteException.Message.Contains(ForbiddenErrorCode, StringComparison.OrdinalIgnoreCase)
                    || remoteException.Message.Contains("Forbidden", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return exception.InnerException != null && IsForbiddenError(exception.InnerException);
    }

    private async System.Threading.Tasks.Task<bool> TryHandleBusinessErrorAsync(Exception exception)
    {
        if (!TryGetBusinessLocalizationKey(exception, out var localizationKey))
        {
            return false;
        }

        await UiMessageService.Error(L[localizationKey],
            options: new System.Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
        return true;
    }

    private static bool TryGetBusinessLocalizationKey(Exception exception, out string localizationKey)
    {
        if (exception is AbpRemoteCallException remoteException
            && !string.IsNullOrWhiteSpace(remoteException.Error?.Code)
            && BusinessErrorLocalizationMap.TryGetValue(remoteException.Error.Code, out localizationKey))
        {
            return true;
        }

        if (exception.InnerException != null)
        {
            return TryGetBusinessLocalizationKey(exception.InnerException, out localizationKey);
        }

        localizationKey = string.Empty;
        return false;
    }
}
