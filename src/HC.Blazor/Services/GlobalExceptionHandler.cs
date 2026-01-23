using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using Volo.Abp.Http.Client;

namespace HC.Blazor.Services;

public class GlobalExceptionHandler
{
    private readonly NavigationManager _navigationManager;
    private readonly IJSRuntime _jsRuntime;
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public GlobalExceptionHandler(
        NavigationManager navigationManager,
        IJSRuntime jsRuntime,
        AuthenticationStateProvider authenticationStateProvider)
    {
        _navigationManager = navigationManager;
        _jsRuntime = jsRuntime;
        _authenticationStateProvider = authenticationStateProvider;
    }

    public async Task HandleExceptionAsync(Exception exception)
    {
        if (exception is AbpRemoteCallException remoteCallException &&
            (remoteCallException.Message.Contains("Unauthorized") ||
             remoteCallException.Message.Contains("401") ||
             remoteCallException.Message.Contains("Token") ||
             remoteCallException.Message.Contains("Authentication")))
        {
            // Force logout for unauthorized access
            await ForceLogoutAsync();
        }
        else
        {
            // Re-throw other exceptions
            throw exception;
        }
    }

    private async Task ForceLogoutAsync()
    {
        try
        {
            // Sign out from authentication
            var authStateProvider = _authenticationStateProvider as dynamic;
            if (authStateProvider?.SignOutAsync != null)
            {
                await authStateProvider.SignOutAsync();
            }

            // Clear any cached data if needed
            await _jsRuntime.InvokeVoidAsync("localStorage.clear");
            await _jsRuntime.InvokeVoidAsync("sessionStorage.clear");

            // Redirect to logout page
            _navigationManager.NavigateTo("/Account/Logout", forceLoad: true);
        }
        catch
        {
            // If logout fails, still redirect
            _navigationManager.NavigateTo("/Account/Logout", forceLoad: true);
        }
    }
}