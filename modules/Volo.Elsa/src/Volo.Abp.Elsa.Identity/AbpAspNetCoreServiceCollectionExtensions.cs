using System;
using Elsa.Identity.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.Security.Claims;

namespace Volo.Abp.Elsa;

public static class AbpAspNetCoreServiceCollectionExtensions
{
    public static IServiceCollection AddElsaJwtBearer(this IServiceCollection services, string jwtBearerScheme = "Bearer")
    {
        services.AddAuthentication().AddJwtBearer(AbpElsaJwtBearerDefaults.ElsaBearerAuthenticationScheme);
        services.AddOptions<JwtBearerOptions>(AbpElsaJwtBearerDefaults.ElsaBearerAuthenticationScheme)
            .Configure<IServiceProvider>((jwtBearerOptions, serviceProvider) =>
            {
                var identityOptions = serviceProvider.GetRequiredService<IOptions<IdentityTokenOptions>>().Value;
                identityOptions.ConfigureJwtBearerOptions(jwtBearerOptions);
                jwtBearerOptions.TokenValidationParameters.NameClaimType = AbpClaimTypes.UserName;
                jwtBearerOptions.MapInboundClaims = false;
            });

        services.Configure<AbpElsaMultipleJwtBearerOptions>(options =>
        {
            options.JwtBearerScheme = jwtBearerScheme;
            options.AbpElsaJwtBearerScheme = AbpElsaJwtBearerDefaults.ElsaBearerAuthenticationScheme;
        });
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, AbpElsaMultipleJwtBearerHandler>(
                AbpElsaJwtBearerDefaults.AuthenticationScheme,
                null,
                options => { });

        return services;
    }
}
