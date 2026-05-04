using Elsa.Features.Abstractions;
using Elsa.Features.Attributes;
using Elsa.Features.Services;
using Elsa.Identity.Contracts;
using Elsa.Identity.Features;
using Elsa.Identity.Options;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.Security.Claims;

namespace Volo.Abp.Elsa;

[DependsOn(typeof(IdentityFeature))]
[PublicAPI]
public class AbpIdentityFeature : FeatureBase
{
    public AbpIdentityFeature(IModule module) : base(module)
    {
    }

    public override void Apply()
    {
        Services.Replace(ServiceDescriptor.Scoped<IUserCredentialsValidator, AbpIdentityUserCredentialsValidator>());
        Services.Replace(ServiceDescriptor.Scoped<IAccessTokenIssuer, AbpIdentityAccessTokenIssuer>());
    }
}
