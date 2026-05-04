using System;
using System.Threading;
using System.Threading.Tasks;
using Elsa.Common;
using Elsa.Identity.Contracts;
using Elsa.Identity.Entities;
using Elsa.Identity.Models;
using Elsa.Identity.Options;
using FastEndpoints.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Volo.Abp.Identity;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace Volo.Abp.Elsa;

public class AbpIdentityAccessTokenIssuer : IAccessTokenIssuer
{
    protected ISystemClock SystemClock  { get; }
    protected IOptions<IdentityTokenOptions> IdentityTokenOptions { get; }
    protected IdentityUserManager UserManager { get; }
    protected IUserClaimsPrincipalFactory<IdentityUser> UserClaimsPrincipalFactory { get; }

    public AbpIdentityAccessTokenIssuer(
        ISystemClock systemClock,
        IOptions<IdentityTokenOptions> identityTokenOptions,
        IdentityUserManager userManager,
        IUserClaimsPrincipalFactory<IdentityUser> userClaimsPrincipalFactory)
    {
        SystemClock = systemClock;
        IdentityTokenOptions = identityTokenOptions;
        UserManager = userManager;
        UserClaimsPrincipalFactory = userClaimsPrincipalFactory;
    }

    public async ValueTask<IssuedTokens> IssueTokensAsync(User user, CancellationToken cancellationToken = default)
    {
        var abpUser = await UserManager.FindByIdAsync(user.Id);
        if (abpUser == null)
        {
            throw new AbpException($"User with ID '{user.Id}' not found.");
        }

        var claimsPrincipal = await UserClaimsPrincipalFactory.CreateAsync(abpUser);

        var tokenOptions = IdentityTokenOptions.Value;
        var signingKey = tokenOptions.SigningKey;
        var issuer = tokenOptions.Issuer;
        var audience = tokenOptions.Audience;
        var accessTokenLifetime = tokenOptions.AccessTokenLifetime;
        var refreshTokenLifetime = tokenOptions.RefreshTokenLifetime;

        Check.NotNullOrWhiteSpace(signingKey, nameof(signingKey));
        Check.NotNullOrWhiteSpace(issuer, nameof(issuer));
        Check.NotNullOrWhiteSpace(audience, nameof(audience));

        var now = SystemClock.UtcNow;
        var accessTokenExpiresAt = now.Add(accessTokenLifetime);
        var refreshTokenExpiresAt = now.Add(refreshTokenLifetime);
        var accessToken = JwtBearer.CreateToken(options => ConfigureTokenOptions(options, accessTokenExpiresAt.UtcDateTime));
        var refreshToken = JwtBearer.CreateToken(options => ConfigureTokenOptions(options, refreshTokenExpiresAt.UtcDateTime));

        return new IssuedTokens(accessToken, refreshToken);

        void ConfigureTokenOptions(JwtCreationOptions options, DateTime expireAt)
        {
            options.SigningKey = signingKey;
            options.ExpireAt = expireAt;
            options.Issuer = issuer;
            options.Audience = audience;
            options.User.Claims.AddRange(claimsPrincipal.Claims);
            //options.User.Permissions.AddRange(permissions);
            //options.User.Roles.AddRange(roleNames);
        }
    }
}
