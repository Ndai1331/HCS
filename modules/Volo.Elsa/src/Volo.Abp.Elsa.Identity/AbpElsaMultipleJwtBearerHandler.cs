using System;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Volo.Abp.Elsa;

public class AbpElsaMultipleJwtBearerHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    protected IAuthenticationHandlerProvider AuthenticationHandlerProvider { get; }
    protected IOptions<AbpElsaMultipleJwtBearerOptions> AbpElsaMultipleJwtBearerOptions { get; }

    [Obsolete]
    public AbpElsaMultipleJwtBearerHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock, IAuthenticationHandlerProvider authenticationHandlerProvider,
        IOptions<AbpElsaMultipleJwtBearerOptions> abpElsaMultipleJwtBearerOptions)
        : base(options, logger, encoder, clock)
    {
        AuthenticationHandlerProvider = authenticationHandlerProvider;
        AbpElsaMultipleJwtBearerOptions = abpElsaMultipleJwtBearerOptions;
    }

    public AbpElsaMultipleJwtBearerHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IAuthenticationHandlerProvider authenticationHandlerProvider,
        IOptions<AbpElsaMultipleJwtBearerOptions> abpElsaMultipleJwtBearerOptions)
        : base(options, logger, encoder)
    {
        AuthenticationHandlerProvider = authenticationHandlerProvider;
        AbpElsaMultipleJwtBearerOptions = abpElsaMultipleJwtBearerOptions;
    }

    protected async override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var schemes = new[]
        {
            AbpElsaMultipleJwtBearerOptions.Value.JwtBearerScheme,
            AbpElsaMultipleJwtBearerOptions.Value.AbpElsaJwtBearerScheme
        };

        foreach (var scheme in schemes)
        {
            var handler = await AuthenticationHandlerProvider.GetHandlerAsync(Context, scheme);
            if (handler == null)
            {
                continue;
            }

            var result = await handler.AuthenticateAsync();
            if (result.Succeeded)
            {
                return result;
            }
        }

        return AuthenticateResult.Fail(new AbpException($"Failed to authenticate using any of the configured JWT bearer schemes: {string.Join(", ", schemes)}"));
    }
}
