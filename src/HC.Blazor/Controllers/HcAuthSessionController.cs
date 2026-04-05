using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace HC.Blazor.Controllers;

/// <summary>
/// After profile updates, Blazor's auth cookie still holds old name/email claims.
/// A short OpenID Connect challenge re-signs the user in and repopulates claims from the userinfo endpoint.
/// </summary>
[Route("hc/auth")]
public class HcAuthSessionController : AbpController
{
    public const string OidcAuthenticationScheme = "oidc";

    [HttpGet("refresh-claims")]
    [Authorize]
    public virtual IActionResult RefreshClaims([FromQuery] string returnUrl = "/my-profile")
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl))
        {
            returnUrl = "/my-profile";
        }

        return Challenge(
            new AuthenticationProperties { RedirectUri = returnUrl },
            OidcAuthenticationScheme);
    }
}
