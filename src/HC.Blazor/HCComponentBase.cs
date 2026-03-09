using System;
using HC.Localization;
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

    protected HCComponentBase()
    {
        LocalizationResource = typeof(HCResource);
    }

    protected override async System.Threading.Tasks.Task HandleErrorAsync(Exception exception)
    {
        if (await TryHandleForbiddenErrorAsync(exception))
        {
            return;
        }

        await base.HandleErrorAsync(exception);
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
