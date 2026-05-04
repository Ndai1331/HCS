using System;
using Elsa.Extensions;
using Elsa.Features.Services;
using Elsa.Identity.Contracts;
using Elsa.Identity.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Volo.Abp.Elsa;

public static class ElsaModuleExtensions
{
    public static IModule UseAbpIdentity(this IModule module, Action<IdentityFeature> configure)
    {
        module.UseIdentity(configure);
        module.Configure<AbpIdentityFeature>();
        return module;
    }

    public static IModule UseAbpIdentity(this IModule module, string signingKey, string issuer = "http://elsa.api", string audience = "http://elsa.api", TimeSpan? tokenLifetime = default)
    {
        module.UseIdentity(signingKey, issuer, audience, tokenLifetime);
        module.Configure<AbpIdentityFeature>();
        return module;
    }
}
