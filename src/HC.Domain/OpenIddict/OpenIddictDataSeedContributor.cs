using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.OpenIddict;
using Volo.Abp.OpenIddict.Applications;
using Volo.Abp.OpenIddict.Scopes;
using Volo.Abp.Uow;

namespace HC.OpenIddict;

/* Creates initial data that is needed to property run the application
 * and make client-to-server communication possible.
 */
public class OpenIddictDataSeedContributor : OpenIddictDataSeedContributorBase, IDataSeedContributor, ITransientDependency
{
    public OpenIddictDataSeedContributor(
        IConfiguration configuration,
        IOpenIddictApplicationRepository openIddictApplicationRepository,
        IAbpApplicationManager applicationManager,
        IOpenIddictScopeRepository openIddictScopeRepository,
        IOpenIddictScopeManager scopeManager)
        : base(configuration, openIddictApplicationRepository, applicationManager, openIddictScopeRepository, scopeManager)
    {
    }

    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        await CreateScopesAsync();
        await CreateApplicationsAsync();
    }

    private async Task CreateScopesAsync()
    {
        await CreateScopesAsync(new OpenIddictScopeDescriptor 
        {
            Name = "HC", 
            DisplayName = "HC API", 
            Resources = { "HC" }
        });
    }

    private async Task CreateApplicationsAsync()
    {
        var commonScopes = new List<string> {
            OpenIddictConstants.Permissions.Scopes.Address,
            OpenIddictConstants.Permissions.Scopes.Email,
            OpenIddictConstants.Permissions.Scopes.Phone,
            OpenIddictConstants.Permissions.Scopes.Profile,
            OpenIddictConstants.Permissions.Scopes.Roles,
            OpenIddictConstants.Permissions.Endpoints.Authorization,

            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
            OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
            "HC"
        };

        var configurationSection = Configuration.GetSection("OpenIddict:Applications");


        //Console Test / Angular Client
        var consoleAndAngularClientId = configurationSection["HC_App:ClientId"];
        if (!consoleAndAngularClientId.IsNullOrWhiteSpace())
        {
            var consoleAndAngularClientRootUrl = configurationSection["HC_App:RootUrl"]?.TrimEnd('/');
            await CreateOrUpdateApplicationAsync(
                applicationType: OpenIddictConstants.ApplicationTypes.Web,
                name: consoleAndAngularClientId!,
                type: OpenIddictConstants.ClientTypes.Public,
                consentType: OpenIddictConstants.ConsentTypes.Implicit,
                displayName: "Console Test / Angular Application",
                secret: null,
                grantTypes: new List<string> {
                    OpenIddictConstants.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.GrantTypes.Password,
                    OpenIddictConstants.GrantTypes.ClientCredentials,
                    OpenIddictConstants.GrantTypes.RefreshToken,
                    "LinkLogin",
                    "Impersonation"
                },
                scopes: commonScopes,
                redirectUris: new List<string> { consoleAndAngularClientRootUrl },
                postLogoutRedirectUris: new List<string> { consoleAndAngularClientRootUrl },
                clientUri: consoleAndAngularClientRootUrl,
                logoUri: "/images/clients/angular.svg"
            );
        }

        
        // Blazor Server Tiered Client
        var blazorServerClientId = configurationSection["HC_BlazorServer:ClientId"];
        if (!blazorServerClientId.IsNullOrWhiteSpace())
        {
            var blazorServerRootUrl = configurationSection["HC_BlazorServer:RootUrl"]!.EnsureEndsWith('/');

            await CreateOrUpdateApplicationAsync(
                applicationType: OpenIddictConstants.ApplicationTypes.Web,
                name: blazorServerClientId!,
                type: OpenIddictConstants.ClientTypes.Confidential,
                consentType: OpenIddictConstants.ConsentTypes.Implicit,
                displayName: "Blazor Server Application",
                secret: configurationSection["HC_BlazorServer:ClientSecret"] ?? "1q2w3e*",
                grantTypes: new List<string> //Hybrid flow
                {
                    OpenIddictConstants.GrantTypes.AuthorizationCode, 
                    OpenIddictConstants.GrantTypes.Implicit
                },
                scopes: commonScopes,
                redirectUris: new List<string> { $"{blazorServerRootUrl}signin-oidc" },
                postLogoutRedirectUris: new List<string> { $"{blazorServerRootUrl}signout-callback-oidc" },
                clientUri: blazorServerRootUrl,
                logoUri: "/images/clients/blazor.svg"
            );
        }
        
        var blazorServerLocalClientId = configurationSection["HC_BlazorServer_Local:ClientId"];
        if (!blazorServerLocalClientId.IsNullOrWhiteSpace())
        {
            var blazorServerLocalRootUrl = configurationSection["HC_BlazorServer_Local:RootUrl"]!.EnsureEndsWith('/');

            await CreateOrUpdateApplicationAsync(
                applicationType: OpenIddictConstants.ApplicationTypes.Web,
                name: blazorServerLocalClientId!,
                type: OpenIddictConstants.ClientTypes.Confidential,
                consentType: OpenIddictConstants.ConsentTypes.Implicit,
                displayName: "Blazor Server Local Application",
                secret: configurationSection["HC_BlazorServer_Local:ClientSecret"] ?? "1q2w3e*",
                grantTypes: new List<string> //Hybrid flow
                {
                    OpenIddictConstants.GrantTypes.AuthorizationCode, OpenIddictConstants.GrantTypes.Implicit
                },
                scopes: commonScopes,
                redirectUris: new List<string> { $"{blazorServerLocalRootUrl}signin-oidc" },
                postLogoutRedirectUris: new List<string> { $"{blazorServerLocalRootUrl}signout-callback-oidc" },
                clientUri: blazorServerLocalRootUrl,
                logoUri: "/images/clients/blazor.svg"
            );
        }




        // // Blazor Server Tiered Client
        // var blazorServerClientId = configurationSection["HC_BlazorServer:ClientId"];
        // if (!blazorServerClientId.IsNullOrWhiteSpace())
        // {
        //     var blazorServerRootUrl = configurationSection["HC_BlazorServer:RootUrl"]!.EnsureEndsWith('/');

        //     // Build redirect URIs list - ensure both formats are included
        //     var redirectUris = new List<string> { $"{blazorServerRootUrl}signin-oidc" };
        //     // Also add common production URL
        //     redirectUris.Add("https://chiso.bvlevanthinh.vn/signin-oidc");

        //     var postLogoutRedirectUris = new List<string> { $"{blazorServerRootUrl}signout-callback-oidc" };
        //     postLogoutRedirectUris.Add("https://chiso.bvlevanthinh.vn/signout-callback-oidc");

        //     await CreateOrUpdateApplicationAsync(
        //         applicationType: OpenIddictConstants.ApplicationTypes.Web,
        //         name: blazorServerClientId!,
        //         type: OpenIddictConstants.ClientTypes.Confidential,
        //         consentType: OpenIddictConstants.ConsentTypes.Implicit,
        //         displayName: "Blazor Server Application",
        //         secret: configurationSection["HC_BlazorServer:ClientSecret"] ?? "1q2w3e*",
                
        //         grantTypes: new List<string>
        //         {
        //             OpenIddictConstants.GrantTypes.AuthorizationCode,
        //             OpenIddictConstants.GrantTypes.Implicit
        //         },
                
        //         scopes: commonScopes,
        //         redirectUris: redirectUris,
        //         postLogoutRedirectUris: postLogoutRedirectUris,
        //         clientUri: blazorServerRootUrl,
        //         logoUri: "/images/clients/blazor.svg"
        //     );
        // }

        // // Blazor Server Production Client
        // var blazorServerProdClientId = configurationSection["HC_BlazorServer_Prod:ClientId"] ?? "HC_BlazorServer_Prod";
        // var blazorServerProdRootUrl = configurationSection["HC_BlazorServer_Prod:RootUrl"]?.EnsureEndsWith('/') ?? "https://chiso.bvlevanthinh.vn/";

        // await CreateOrUpdateApplicationAsync(
        //     applicationType: OpenIddictConstants.ApplicationTypes.Web,
        //     name: blazorServerProdClientId,
        //     type: OpenIddictConstants.ClientTypes.Confidential,
        //     consentType: OpenIddictConstants.ConsentTypes.Implicit,
        //     displayName: "Blazor Server Production Application",
        //     secret: configurationSection["HC_BlazorServer_Prod:ClientSecret"] ?? "1q2w3e*",
            
        //     // PRODUCTION: Use Authorization Code Flow only (production-safe)
        //     // response_type=code (Authorization Code Flow) - REQUIRED for production
        //     // DO NOT use Hybrid Flow (code id_token) or Implicit Flow
        //     grantTypes: new List<string>
        //     {
        //         OpenIddictConstants.GrantTypes.AuthorizationCode // Authorization Code Flow
        //     },
            
        //     scopes: commonScopes,
        //     redirectUris: new List<string> 
        //     { 
        //         $"{blazorServerProdRootUrl}signin-oidc",
        //         "https://chiso.bvlevanthinh.vn/signin-oidc",
        //         // Allow HTTP in case reverse proxy terminates TLS before reaching AuthServer
        //         "http://chiso.bvlevanthinh.vn/signin-oidc"
        //     },
        //     postLogoutRedirectUris: new List<string> 
        //     { 
        //         $"{blazorServerProdRootUrl}signout-callback-oidc",
        //         "https://chiso.bvlevanthinh.vn/signout-callback-oidc",
        //         "http://chiso.bvlevanthinh.vn/signout-callback-oidc"
        //     },
        //     clientUri: blazorServerProdRootUrl,
        //     logoUri: "/images/clients/blazor.svg"
        // );


        // Swagger Client
        var swaggerClientId = configurationSection["HC_Swagger:ClientId"];
        if (!swaggerClientId.IsNullOrWhiteSpace())
        {
            var swaggerRootUrl = configurationSection["HC_Swagger:RootUrl"]?.TrimEnd('/');

            await CreateOrUpdateApplicationAsync(
                applicationType: OpenIddictConstants.ApplicationTypes.Web,
                name: swaggerClientId!,
                type: OpenIddictConstants.ClientTypes.Public,
                consentType: OpenIddictConstants.ConsentTypes.Implicit,
                displayName: "Swagger Application",
                secret: null,
                grantTypes: new List<string> { OpenIddictConstants.GrantTypes.AuthorizationCode, },
                scopes: commonScopes,
                redirectUris: new List<string> { $"{swaggerRootUrl}/swagger/oauth2-redirect.html" },
                clientUri: swaggerRootUrl.EnsureEndsWith('/') + "swagger",
                logoUri: "/images/clients/swagger.svg"
            );
        }


    }
}
